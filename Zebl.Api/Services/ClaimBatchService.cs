using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

public class ClaimBatchService : IClaimBatchService
{
    private readonly ZeblDbContext _db;
    private readonly Zebl.Application.Services.Edi.IEdiGenerator _ediGenerator;
    private readonly ISendingClaimsSettingsService _sendingClaimsSettingsService;
    private readonly IClearinghouseClient _clearinghouseClient;
    private readonly Zebl.Application.Repositories.IConnectionLibraryRepository _connectionLibraryRepository;
    private readonly Zebl.Infrastructure.Services.SftpTransportService _sftpTransportService;
    private readonly ILogger<ClaimBatchService> _logger;

    private const string BatchStatusDraft = "Draft";
    private const string BatchStatusProcessing = "Processing";
    private const string BatchStatusCompleted = "Completed";
    private const string BatchStatusFailed = "Failed";
    private const string BatchStatusPartial = "Partial";
    private const string BatchItemStatusPending = "Pending";
    private const string BatchItemStatusSuccess = "Success";
    private const string BatchItemStatusFailed = "Failed";
    private const string SubmissionMarkerSuccess = "SUCCESS";
    private const string SubmissionMarkerFailed = "FAILED";

    public ClaimBatchService(
        ZeblDbContext db,
        Zebl.Application.Services.Edi.IEdiGenerator ediGenerator,
        ISendingClaimsSettingsService sendingClaimsSettingsService,
        IClearinghouseClient clearinghouseClient,
        Zebl.Application.Repositories.IConnectionLibraryRepository connectionLibraryRepository,
        Zebl.Infrastructure.Services.SftpTransportService sftpTransportService,
        ILogger<ClaimBatchService> logger)
    {
        _db = db;
        _ediGenerator = ediGenerator;
        _sendingClaimsSettingsService = sendingClaimsSettingsService;
        _clearinghouseClient = clearinghouseClient;
        _connectionLibraryRepository = connectionLibraryRepository;
        _sftpTransportService = sftpTransportService;
        _logger = logger;
    }

    public async Task<BatchCreationResult> CreateBatchAsync(CreateBatchRequest request, CancellationToken cancellationToken)
    {
        var claimIds = request.ClaimIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var key = NormalizeIdempotencyKey(request.IdempotencyKey);
            var existingBatch = await _db.ClaimBatches
                .AsNoTracking()
                .Where(b =>
                    b.TenantId == request.TenantId &&
                    b.FacilityId == request.FacilityId &&
                    b.IdempotencyKey == key)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingBatch != null)
            {
                return new BatchCreationResult
                {
                    BatchId = existingBatch.Id,
                    IsIdempotentHit = true,
                    TotalRequestedClaims = claimIds.Count
                };
            }
        }

        var submittedStatus = ClaimStatusCatalog.ToStorage(ClaimStatus.Submitted);
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-5);
        var cutoffDate = DateOnly.FromDateTime(cutoffUtc);
        var rtsStatus = ClaimStatusCatalog.ToStorage(ClaimStatus.RTS);
        var settings = await _sendingClaimsSettingsService.GetSettingsAsync(request.TenantId, request.FacilityId, cancellationToken);

        var precheckRows = await _db.Claims
            .AsNoTracking()
            .Where(c =>
                claimIds.Contains(c.ClaID) &&
                c.TenantId == request.TenantId &&
                c.FacilityId == request.FacilityId)
            .Select(c => new
            {
                c.ClaID,
                c.ClaStatus,
                c.ClaSubmissionMethod,
                c.ClaLastExportedDate,
                LastSubmissionUtc = _db.ClaimSubmissions
                    .AsNoTracking()
                    .Where(s => s.ClaimId == c.ClaID)
                    .Select(s => (DateTime?)s.SubmissionDate)
                    .OrderByDescending(d => d)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var processingClaimIds = new HashSet<int>(
            await _db.ClaimBatchItems
                .AsNoTracking()
                .Where(i =>
                    i.TenantId == request.TenantId &&
                    i.FacilityId == request.FacilityId &&
                    claimIds.Contains(i.ClaimId) &&
                    i.Batch.Status == BatchStatusProcessing)
                .Select(i => i.ClaimId)
                .Distinct()
                .ToListAsync(cancellationToken));

        var sendableClaimIds = await _db.Claims
            .AsNoTracking()
            .WhereEligibleForSend(request.TenantId, request.FacilityId, rtsStatus, settings.ShowBillToPatientClaims)
            .Where(c => claimIds.Contains(c.ClaID))
            .Select(c => c.ClaID)
            .ToListAsync(cancellationToken);
        var sendableClaimIdSet = sendableClaimIds.ToHashSet();

        var precheckById = precheckRows.ToDictionary(x => x.ClaID, x => x);
        var blockedClaims = new List<BlockedClaimResult>();
        var processableClaimIds = new List<int>();

        foreach (var claimId in claimIds)
        {
            if (processingClaimIds.Contains(claimId))
            {
                blockedClaims.Add(new BlockedClaimResult
                {
                    ClaimId = claimId,
                    Reason = "Claim is already in another processing batch."
                });
                continue;
            }

            if (!precheckById.TryGetValue(claimId, out var row))
            {
                blockedClaims.Add(new BlockedClaimResult
                {
                    ClaimId = claimId,
                    Reason = "Not found in current tenant/facility."
                });
                continue;
            }

            if (!sendableClaimIdSet.Contains(claimId))
            {
                blockedClaims.Add(new BlockedClaimResult
                {
                    ClaimId = claimId,
                    Reason =
                        "Claim is not eligible for send (requires RTS, electronic submission, bill-to other than patient, and primary payer with electronic submission and payer ID)."
                });
                continue;
            }

            if (!request.ForceResubmit)
            {
                var recentlyExported =
                    (row.ClaLastExportedDate.HasValue && row.ClaLastExportedDate.Value >= cutoffDate) ||
                    (row.LastSubmissionUtc.HasValue && row.LastSubmissionUtc.Value >= cutoffUtc);

                if (string.Equals(row.ClaStatus, submittedStatus, StringComparison.Ordinal) || recentlyExported)
                {
                    blockedClaims.Add(new BlockedClaimResult
                    {
                        ClaimId = claimId,
                        Reason = "Already submitted or recently exported."
                    });
                    continue;
                }
            }

            processableClaimIds.Add(claimId);
        }

        var selectedSubmitterReceiverId = await ResolveSubmitterReceiverIdForBatchAsync(
            request.SubmitterReceiverId,
            request.TenantId,
            request.FacilityId,
            cancellationToken);

        var batch = new ClaimBatch
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            FacilityId = request.FacilityId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy,
            Status = BatchStatusDraft,
            TotalClaims = 0,
            SuccessCount = 0,
            FailureCount = 0,
            SubmissionNumber = settings.NextSubmissionNumber,
            IdempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey),
            SubmitterReceiverId = selectedSubmitterReceiverId,
            ConnectionType = NormalizeConnectionType(request.ConnectionType),
            ConnectionLibraryId = request.ConnectionLibraryId
        };

        _db.ClaimBatches.Add(batch);
        if (processableClaimIds.Count > 0)
        {
            var createdAt = DateTime.UtcNow;
            foreach (var claimId in processableClaimIds)
            {
                _db.ClaimBatchItems.Add(new ClaimBatchItem
                {
                    BatchId = batch.Id,
                    ClaimId = claimId,
                    TenantId = request.TenantId,
                    FacilityId = request.FacilityId,
                    Status = BatchItemStatusPending,
                    CreatedAt = createdAt
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        return new BatchCreationResult
        {
            BatchId = batch.Id,
            IsIdempotentHit = false,
            BlockedClaims = blockedClaims,
            TotalRequestedClaims = claimIds.Count
        };
    }

    public async Task<BatchProcessResult> ProcessBatchAsync(ProcessBatchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "ProcessBatchAsync start. BatchId={BatchId}, Tenant={TenantId}, Facility={FacilityId}",
            request.BatchId,
            request.TenantId,
            request.FacilityId);
        var batch = await _db.ClaimBatches
            .FirstOrDefaultAsync(b => b.Id == request.BatchId && b.TenantId == request.TenantId && b.FacilityId == request.FacilityId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (batch.Status == BatchStatusProcessing)
            throw new InvalidOperationException("Batch is already processing.");

        await SetBatchStatusWithConcurrencyAsync(batch, BatchStatusProcessing, cancellationToken);

        var failedClaims = new List<FailedClaimResult>();
        var submittedStatus = ClaimStatusCatalog.ToStorage(ClaimStatus.Submitted);
        if (!batch.SubmitterReceiverId.HasValue)
            throw new InvalidOperationException("SubmitterReceiverId is required on the batch.");
        var receiverLibraryId = batch.SubmitterReceiverId.Value;
        var pendingItems = await _db.ClaimBatchItems
            .Where(i => i.BatchId == request.BatchId && i.Status == BatchItemStatusPending)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
        _logger.LogInformation("ProcessBatchAsync pending items loaded. BatchId={BatchId}, PendingCount={PendingCount}", request.BatchId, pendingItems.Count);
        if (pendingItems.Count == 0)
        {
            throw new InvalidOperationException("No eligible claims were queued for this batch.");
        }

        var successful837Bodies = new List<string>();
        foreach (var item in pendingItems)
        {
            _logger.LogInformation("ProcessBatchAsync processing item. BatchId={BatchId}, ClaimId={ClaimId}, ItemId={ItemId}", request.BatchId, item.ClaimId, item.Id);
            var claimResult = await ProcessBatchItemAsync(
                request.BatchId,
                item,
                item.ClaimId,
                request.TenantId,
                request.FacilityId,
                receiverLibraryId,
                submittedStatus,
                cancellationToken);

            if (!claimResult.Success)
            {
                _logger.LogWarning(
                    "ProcessBatchAsync item failed. BatchId={BatchId}, ClaimId={ClaimId}, Error={Error}",
                    request.BatchId,
                    item.ClaimId,
                    claimResult.ErrorMessage);
                failedClaims.Add(new FailedClaimResult { ClaimId = item.ClaimId, ErrorMessage = claimResult.ErrorMessage });
            }
            else if (!string.IsNullOrEmpty(claimResult.Edi837))
            {
                successful837Bodies.Add(claimResult.Edi837);
            }
        }
        _logger.LogInformation(
            "ProcessBatchAsync loop complete. BatchId={BatchId}, Successful837Count={Successful837Count}, FailedCount={FailedCount}",
            request.BatchId,
            successful837Bodies.Count,
            failedClaims.Count);

        try
        {
            _logger.LogInformation(
                "ProcessBatchAsync before post-processing. BatchId={BatchId}, ConnectionType={ConnectionType}, Successful837Count={Successful837Count}",
                request.BatchId,
                batch.ConnectionType,
                successful837Bodies.Count);
            await ApplyConnectionTypePostProcessingAsync(batch, successful837Bodies, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessBatchAsync post-processing failed. BatchId={BatchId}", request.BatchId);
            await MarkBatchFailedAsync(batch, $"Batch post-processing failed: {ex.Message}", cancellationToken);
        }

        var refreshed = await RecalculateBatchCountsAndStatusAsync(batch.Id, request.TenantId, request.FacilityId, cancellationToken);
        if (refreshed.SuccessCount > 0)
        {
            await _sendingClaimsSettingsService.GetAndIncrementSubmissionNumberAsync(request.TenantId, request.FacilityId, cancellationToken);
        }
        return new BatchProcessResult
        {
            BatchId = batch.Id,
            TotalClaims = refreshed.TotalClaims,
            SuccessCount = refreshed.SuccessCount,
            FailureCount = refreshed.FailureCount,
            FailedClaims = failedClaims
        };
    }

    public async Task<BatchProcessResult> RetryBatchAsync(RetryBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await _db.ClaimBatches
            .FirstOrDefaultAsync(b => b.Id == request.BatchId && b.TenantId == request.TenantId && b.FacilityId == request.FacilityId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (batch.Status == BatchStatusProcessing)
            throw new InvalidOperationException("Batch is already processing.");

        await SetBatchStatusWithConcurrencyAsync(batch, BatchStatusProcessing, cancellationToken);

        var submittedStatus = ClaimStatusCatalog.ToStorage(ClaimStatus.Submitted);
        if (!batch.SubmitterReceiverId.HasValue)
            throw new InvalidOperationException("SubmitterReceiverId is required on the batch.");
        var receiverLibraryId = batch.SubmitterReceiverId.Value;
        var failedClaims = new List<FailedClaimResult>();
        var retryItems = await _db.ClaimBatchItems
            .Where(i => i.BatchId == request.BatchId && i.Status == BatchItemStatusFailed)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in retryItems)
        {
            item.Status = BatchItemStatusPending;
            item.ErrorMessage = null;
        }
        if (retryItems.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        var successful837Bodies = new List<string>();
        foreach (var item in retryItems)
        {
            var claimResult = await ProcessBatchItemAsync(
                request.BatchId,
                item,
                item.ClaimId,
                request.TenantId,
                request.FacilityId,
                receiverLibraryId,
                submittedStatus,
                cancellationToken);

            if (!claimResult.Success)
            {
                failedClaims.Add(new FailedClaimResult { ClaimId = item.ClaimId, ErrorMessage = claimResult.ErrorMessage });
            }
            else if (!string.IsNullOrEmpty(claimResult.Edi837))
            {
                successful837Bodies.Add(claimResult.Edi837);
            }
        }

        try
        {
            await ApplyConnectionTypePostProcessingAsync(batch, successful837Bodies, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkBatchFailedAsync(batch, $"Batch post-processing failed: {ex.Message}", cancellationToken);
        }

        var refreshed = await RecalculateBatchCountsAndStatusAsync(batch.Id, request.TenantId, request.FacilityId, cancellationToken);
        if (refreshed.SuccessCount > 0)
        {
            await _sendingClaimsSettingsService.GetAndIncrementSubmissionNumberAsync(request.TenantId, request.FacilityId, cancellationToken);
        }
        return new BatchProcessResult
        {
            BatchId = batch.Id,
            TotalClaims = refreshed.TotalClaims,
            SuccessCount = refreshed.SuccessCount,
            FailureCount = refreshed.FailureCount,
            FailedClaims = failedClaims
        };
    }

    public async Task<BatchDetailResult?> GetBatchAsync(GetBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await _db.ClaimBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BatchId && b.TenantId == request.TenantId && b.FacilityId == request.FacilityId, cancellationToken);
        if (batch == null) return null;

        var items = await _db.ClaimBatchItems
            .AsNoTracking()
            .Where(i => i.BatchId == request.BatchId)
            .OrderBy(i => i.Id)
            .Select(i => new BatchItemResult
            {
                Id = i.Id,
                ClaimId = i.ClaimId,
                Status = i.Status,
                ErrorMessage = i.ErrorMessage,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new BatchDetailResult
        {
            Id = batch.Id,
            Status = batch.Status,
            SubmissionNumber = batch.SubmissionNumber,
            SubmitterReceiverId = batch.SubmitterReceiverId,
            ConnectionType = batch.ConnectionType,
            TotalClaims = batch.TotalClaims,
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            CreatedAt = batch.CreatedAt,
            SubmittedAt = batch.SubmittedAt,
            FilePath = batch.FilePath,
            Items = items
        };
    }

    public async Task<BatchListResult> GetBatchesAsync(GetBatchesRequest request, CancellationToken cancellationToken)
    {
        var query = _db.ClaimBatches
            .AsNoTracking()
            .Where(b => b.TenantId == request.TenantId && b.FacilityId == request.FacilityId)
            .OrderByDescending(b => b.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new BatchListItemResult
            {
                Id = b.Id,
                Status = b.Status,
                SubmissionNumber = b.SubmissionNumber,
                SubmitterReceiverId = b.SubmitterReceiverId,
                ConnectionType = b.ConnectionType,
                TotalClaims = b.TotalClaims,
                SuccessCount = b.SuccessCount,
                FailureCount = b.FailureCount,
                CreatedAt = b.CreatedAt,
                SubmittedAt = b.SubmittedAt,
                FilePath = b.FilePath
            })
            .ToListAsync(cancellationToken);

        return new BatchListResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<BatchEdiResult> GetBatchEdiAsync(GetBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await _db.ClaimBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.Id == request.BatchId &&
                b.TenantId == request.TenantId &&
                b.FacilityId == request.FacilityId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (!batch.SubmitterReceiverId.HasValue)
            throw new InvalidOperationException("SubmitterReceiverId is required on the batch.");
        var receiverLibraryId = batch.SubmitterReceiverId.Value;

        var claimIds = await _db.ClaimBatchItems
            .AsNoTracking()
            .Where(i => i.BatchId == request.BatchId)
            .OrderBy(i => i.Id)
            .Select(i => i.ClaimId)
            .ToListAsync(cancellationToken);

        var claimInterchanges = new List<string>();
        foreach (var claimId in claimIds)
        {
            var edi = await _ediGenerator.GenerateAsync(
                receiverLibraryId,
                claimId,
                Zebl.Application.Services.Edi.OutboundEdiKind.Claim837,
                cancellationToken);
            claimInterchanges.Add(edi);
        }

        var batchInterchange = claimInterchanges.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, claimInterchanges);

        return new BatchEdiResult
        {
            BatchId = batch.Id,
            EdiContent = batchInterchange
        };
    }

    public async Task<BatchZipResult> ExportBatchZipAsync(GetBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await _db.ClaimBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.Id == request.BatchId &&
                b.TenantId == request.TenantId &&
                b.FacilityId == request.FacilityId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (!batch.SubmitterReceiverId.HasValue)
            throw new InvalidOperationException("SubmitterReceiverId is required on the batch.");
        var receiverLibraryId = batch.SubmitterReceiverId.Value;

        var claimIds = await _db.ClaimBatchItems
            .AsNoTracking()
            .Where(i => i.BatchId == request.BatchId)
            .OrderBy(i => i.Id)
            .Select(i => i.ClaimId)
            .ToListAsync(cancellationToken);

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var claimId in claimIds)
            {
                var edi = await _ediGenerator.GenerateAsync(
                    receiverLibraryId,
                    claimId,
                    Zebl.Application.Services.Edi.OutboundEdiKind.Claim837,
                    cancellationToken);
                var entry = archive.CreateEntry($"claim-{claimId}.837", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                await writer.WriteAsync(edi);
                await writer.FlushAsync();
            }
        }

        return new BatchZipResult
        {
            BatchId = batch.Id,
            Content = stream.ToArray(),
            FileName = $"batch-{batch.Id:N}.zip"
        };
    }

    private async Task SetBatchStatusWithConcurrencyAsync(ClaimBatch batch, string status, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                batch.Status = status;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                _db.Entry(batch).Reload();
                if (batch.Status == BatchStatusProcessing)
                    throw new InvalidOperationException("Batch is already processing.");
            }
        }

        throw new InvalidOperationException("Could not transition batch status due to concurrent updates.");
    }

    private async Task<(int TotalClaims, int SuccessCount, int FailureCount)> RecalculateBatchCountsAndStatusAsync(
        Guid batchId,
        int tenantId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var counts = await _db.ClaimBatchItems
            .Where(i => i.BatchId == batchId && i.TenantId == tenantId && i.FacilityId == facilityId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Success = g.Count(x => x.Status == BatchItemStatusSuccess),
                Failed = g.Count(x => x.Status == BatchItemStatusFailed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalClaims = counts?.Total ?? 0;
        var successCount = counts?.Success ?? 0;
        var failureCount = counts?.Failed ?? 0;
        var status = ResolveFinalBatchStatus(totalClaims, successCount, failureCount);

        var batch = await _db.ClaimBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId && b.FacilityId == facilityId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        batch.TotalClaims = totalClaims;
        batch.SuccessCount = successCount;
        batch.FailureCount = failureCount;
        batch.Status = status;
        batch.SubmittedAt = totalClaims > 0 ? DateTime.UtcNow : null;

        await _db.SaveChangesAsync(cancellationToken);
        return (totalClaims, successCount, failureCount);
    }

    private async Task ApplyConnectionTypePostProcessingAsync(
        ClaimBatch batch,
        IReadOnlyList<string> successful837Bodies,
        CancellationToken cancellationToken)
    {
        if (string.Equals(batch.ConnectionType, "Export", StringComparison.OrdinalIgnoreCase))
        {
            if (successful837Bodies.Count == 0)
                return;

            var batchInterchange = string.Join(Environment.NewLine, successful837Bodies);
            var path = await SaveBatch837FileAsync(batch.TenantId, batch.Id, batchInterchange, cancellationToken);
            _logger.LogInformation("EDI file created at {Path}", path);
            batch.FilePath = path;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.Equals(batch.ConnectionType, "Clearinghouse", StringComparison.OrdinalIgnoreCase))
        {
            if (successful837Bodies.Count == 0)
            {
                _logger.LogWarning(
                    "Skipping clearinghouse submission: no successful 837 bodies. BatchId={BatchId}",
                    batch.Id);
                return;
            }

            var ediContent = string.Join(Environment.NewLine, successful837Bodies);
            batch.SentEdiContent = ediContent;
            var path = await SaveBatch837FileAsync(batch.TenantId, batch.Id, ediContent, cancellationToken);
            _logger.LogInformation("EDI file created at {Path}", path);
            batch.FilePath = path;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Submitting batch to clearinghouse transport. BatchId={BatchId}, ConnectionLibraryId={ConnectionLibraryId}",
                batch.Id,
                batch.ConnectionLibraryId);
            var submission = await SubmitUsingConnectionLibraryAsync(batch, ediContent, cancellationToken);

            if (!submission.Success)
            {
                var message = TruncateSafe($"Clearinghouse upload failed: {submission.Message}", 500);

                var successfulItems = await _db.ClaimBatchItems
                    .Where(i => i.BatchId == batch.Id && i.Status == BatchItemStatusSuccess)
                    .ToListAsync(cancellationToken);

                foreach (var item in successfulItems)
                {
                    item.Status = BatchItemStatusFailed;
                    item.ErrorMessage = message;
                }

                batch.Status = BatchStatusFailed;
                batch.SubmittedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            batch.SubmittedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<SubmissionResult> SubmitUsingConnectionLibraryAsync(ClaimBatch batch, string ediContent, CancellationToken cancellationToken)
    {
        if (!batch.ConnectionLibraryId.HasValue || batch.ConnectionLibraryId.Value == Guid.Empty)
            return new SubmissionResult { Success = false, Message = "ConnectionLibraryId is required for clearinghouse submission." };

        var connection = await _connectionLibraryRepository.GetByIdAsync(batch.ConnectionLibraryId.Value);
        if (connection == null)
            return new SubmissionResult { Success = false, Message = "Selected connection library was not found." };
        if (!connection.IsActive)
            return new SubmissionResult { Success = false, Message = "Selected connection library is inactive." };

        if (connection.ConnectionType != Zebl.Application.Domain.ConnectionType.Sftp)
            return new SubmissionResult { Success = false, Message = "Clearinghouse batch upload requires ConnectionType SFTP." };

        try
        {
            await _sftpTransportService.UploadFileAsync(connection, $"batch-{batch.Id:N}.837", ediContent);
            return new SubmissionResult { Success = true, Message = "Uploaded via connection library." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection-library upload failed for batch {BatchId}", batch.Id);
            return new SubmissionResult { Success = false, Message = ex.Message };
        }
    }

    private async Task MarkBatchFailedAsync(ClaimBatch batch, string errorMessage, CancellationToken cancellationToken)
    {
        var safeMessage = TruncateSafe(errorMessage, 500);
        var successfulItems = await _db.ClaimBatchItems
            .Where(i => i.BatchId == batch.Id && i.Status == BatchItemStatusSuccess)
            .ToListAsync(cancellationToken);
        foreach (var item in successfulItems)
        {
            item.Status = BatchItemStatusFailed;
            item.ErrorMessage = safeMessage;
        }

        batch.Status = BatchStatusFailed;
        batch.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes one batch file to /exports/{tenantId}/{batchId}.837 (Unix) or a writable ProgramData path on Windows.
    /// </summary>
    private static async Task<string> SaveBatch837FileAsync(
        int tenantId,
        Guid batchId,
        string interchangeBody,
        CancellationToken cancellationToken)
    {
        var directory = GetBatch837ExportDirectory(tenantId);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{batchId:N}.837");
        await File.WriteAllTextAsync(filePath, interchangeBody, cancellationToken);
        return Path.GetFullPath(filePath);
    }

    /// <summary>Directory /exports/{tenantId}/ (root on Unix; on Windows resolves under current drive, e.g. C:\exports\...).</summary>
    private static string GetBatch837ExportDirectory(int tenantId) =>
        Path.Combine("/exports", tenantId.ToString());

    private async Task<(bool Success, string ErrorMessage, string? Edi837)> ProcessBatchItemAsync(
        Guid batchId,
        ClaimBatchItem item,
        int claimId,
        int tenantId,
        int facilityId,
        Guid receiverLibraryId,
        string submittedStatus,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var claimLockedByAnotherBatch = await _db.ClaimBatchItems
                    .AsNoTracking()
                    .Where(i =>
                        i.ClaimId == claimId &&
                        i.BatchId != batchId &&
                        i.TenantId == tenantId &&
                        i.FacilityId == facilityId &&
                        i.Batch.Status == BatchStatusProcessing)
                    .AnyAsync(cancellationToken);

                if (claimLockedByAnotherBatch)
                {
                    var message = "Claim is currently processing in another batch.";
                    item.Status = BatchItemStatusFailed;
                    item.ErrorMessage = TruncateSafe(message, 500);
                    _db.ClaimSubmissions.Add(new ClaimSubmission
                    {
                        ClaimId = claimId,
                        BatchId = batchId.ToString(),
                        SubmissionDate = DateTime.UtcNow,
                        TransactionControlNumber = BuildSubmissionTransactionControlNumber(claimId),
                        FileControlNumber = SubmissionMarkerFailed,
                        PatientControlNumber = TruncateSafe(message, 50)
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return (false, message, null);
                }

                var claim = await _db.Claims
                    .FirstOrDefaultAsync(c =>
                        c.ClaID == claimId &&
                        c.TenantId == tenantId &&
                        c.FacilityId == facilityId, cancellationToken);

                if (claim == null)
                {
                    var message = "Tenant/facility mismatch detected during processing.";
                    item.Status = BatchItemStatusFailed;
                    item.ErrorMessage = TruncateSafe(message, 500);
                    _db.ClaimSubmissions.Add(new ClaimSubmission
                    {
                        ClaimId = claimId,
                        BatchId = batchId.ToString(),
                        SubmissionDate = DateTime.UtcNow,
                        TransactionControlNumber = BuildSubmissionTransactionControlNumber(claimId),
                        FileControlNumber = SubmissionMarkerFailed,
                        PatientControlNumber = TruncateSafe(message, 50)
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return (false, message, null);
                }

                var edi837 = await _ediGenerator.GenerateAsync(
                    receiverLibraryId,
                    claimId,
                    Zebl.Application.Services.Edi.OutboundEdiKind.Claim837,
                    cancellationToken);

                claim.ClaStatus = submittedStatus;
                claim.ClaLastExportedDate = DateOnly.FromDateTime(DateTime.UtcNow);
                claim.ClaBillDate = DateOnly.FromDateTime(DateTime.UtcNow);
                item.Status = BatchItemStatusSuccess;
                item.ErrorMessage = null;

                var latestSubmission = await _db.ClaimSubmissions
                    .Where(s => s.ClaimId == claimId)
                    .OrderByDescending(s => s.SubmissionDate)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestSubmission != null)
                {
                    latestSubmission.BatchId = batchId.ToString();
                    latestSubmission.FileControlNumber = SubmissionMarkerSuccess;
                }
                else
                {
                    _db.ClaimSubmissions.Add(new ClaimSubmission
                    {
                        ClaimId = claimId,
                        BatchId = batchId.ToString(),
                        SubmissionDate = DateTime.UtcNow,
                        TransactionControlNumber = BuildSubmissionTransactionControlNumber(claimId),
                        FileControlNumber = SubmissionMarkerSuccess,
                        PatientControlNumber = BuildExportFileName(claimId, DateTime.UtcNow)
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return (true, string.Empty, edi837);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();

                var safeError = BuildSafeErrorMessage(ex.Message);
                try
                {
                    var failedItem = await _db.ClaimBatchItems
                        .FirstOrDefaultAsync(i => i.Id == item.Id, cancellationToken);
                    if (failedItem != null)
                    {
                        failedItem.Status = BatchItemStatusFailed;
                        failedItem.ErrorMessage = TruncateSafe(safeError, 500);
                    }

                    _db.ClaimSubmissions.Add(new ClaimSubmission
                    {
                        ClaimId = claimId,
                        BatchId = batchId.ToString(),
                        SubmissionDate = DateTime.UtcNow,
                        TransactionControlNumber = BuildSubmissionTransactionControlNumber(claimId),
                        FileControlNumber = SubmissionMarkerFailed,
                        PatientControlNumber = TruncateSafe(safeError, 50)
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception persistEx)
                {
                    _logger.LogError(persistEx, "Failed to persist failure state for claim {ClaimId} in batch {BatchId}.", claimId, batchId);
                }

                _logger.LogError(ex, "Claim batch item processing failed for claim {ClaimId} in batch {BatchId}.", claimId, batchId);
                return (false, safeError, null);
            }
        });
    }

    private static string ResolveFinalBatchStatus(int total, int success, int failed)
    {
        if (total == 0)
            return BatchStatusCompleted;
        if (success == total)
            return BatchStatusCompleted;
        if (failed == total)
            return BatchStatusFailed;
        return BatchStatusPartial;
    }

    private static string BuildSubmissionTransactionControlNumber(int claimId)
        => $"B{claimId:D6}{Guid.NewGuid():N}"[..20];

    private static string BuildExportFileName(int claimId, DateTime utcNow)
        => TruncateSafe($"CLM{claimId}_{utcNow:yyyyMMddHHmmss}.837", 50);

    private static string BuildSafeErrorMessage(string? message)
        => TruncateSafe(message, 200);

    private static string? NormalizeIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        return TruncateSafe(key, 100);
    }

    private static string TruncateSafe(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NormalizeConnectionType(string? connectionType)
    {
        if (string.Equals(connectionType, "Clearinghouse", StringComparison.OrdinalIgnoreCase))
            return "Clearinghouse";
        if (string.Equals(connectionType, "Export", StringComparison.OrdinalIgnoreCase))
            return "Export";
        throw new ArgumentException("ConnectionType must be \"Export\" or \"Clearinghouse\".", nameof(connectionType));
    }

    private async Task<Guid> ResolveSubmitterReceiverIdForBatchAsync(
        Guid? requestedId,
        int tenantId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        if (!requestedId.HasValue)
            throw new ArgumentException("SubmitterReceiverId is required.", "SubmitterReceiverId");

        var selected = await _db.ReceiverLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestedId.Value, cancellationToken);
        if (selected == null)
            throw new ArgumentException("Submitter/Receiver configuration not found.");

        var scopedMismatch =
            (selected.TenantId.HasValue && selected.TenantId.Value != tenantId) ||
            (selected.FacilityId.HasValue && selected.FacilityId.Value != facilityId);
        if (scopedMismatch)
            throw new ArgumentException("Submitter/Receiver does not belong to the current tenant/facility.");

        return selected.Id;
    }

}
