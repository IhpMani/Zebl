using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Services;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using Zebl.Api.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/claims")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class ClaimsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly ICurrentContext _currentContext;
        private readonly ISecondaryTriggerService _secondaryTriggerService;
        private readonly ILogger<ClaimsController> _logger;
        private readonly Zebl.Infrastructure.Services.ProgramSettingsService _programSettingsService;
        private readonly Zebl.Application.Repositories.IClaimRejectionRepository _claimRejectionRepository;
        private readonly IClaimScrubService _claimScrubService;
        private readonly IClaimBatchService _claimBatchService;
        private readonly ISendingClaimsSettingsService _sendingClaimsSettingsService;
        private readonly Zebl.Application.Repositories.IServiceLineRepository _serviceLineRepository;

        public ClaimsController(
            ZeblDbContext db,
            ICurrentUserContext userContext,
            ICurrentContext currentContext,
            ISecondaryTriggerService secondaryTriggerService,
            ILogger<ClaimsController> logger,
            Zebl.Infrastructure.Services.ProgramSettingsService programSettingsService,
            Zebl.Application.Repositories.IClaimRejectionRepository claimRejectionRepository,
            IClaimScrubService claimScrubService,
            IClaimBatchService claimBatchService,
            ISendingClaimsSettingsService sendingClaimsSettingsService,
            Zebl.Application.Repositories.IServiceLineRepository serviceLineRepository)
        {
            _db = db;
            _userContext = userContext;
            _currentContext = currentContext;
            _secondaryTriggerService = secondaryTriggerService;
            _logger = logger;
            _programSettingsService = programSettingsService;
            _claimRejectionRepository = claimRejectionRepository;
            _claimScrubService = claimScrubService;
            _claimBatchService = claimBatchService;
            _sendingClaimsSettingsService = sendingClaimsSettingsService;
            _serviceLineRepository = serviceLineRepository;
        }

        [HttpGet("rejections")]
        public async Task<IActionResult> GetRejections()
        {
            var items = await _claimRejectionRepository.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("rejections/{id:int}")]
        public async Task<IActionResult> GetRejectionById(int id)
        {
            var item = await _claimRejectionRepository.GetByIdAsync(id);
            if (item == null)
                return NotFound();
            return Ok(item);
        }

        [HttpPost("rejections/{id:int}/resolve")]
        public async Task<IActionResult> ResolveRejection(int id)
        {
            var existing = await _claimRejectionRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound();

            existing.Status = "Resolved";
            existing.ResolvedAt = DateTime.UtcNow;
            await _claimRejectionRepository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpPost("scrub")]
        public async Task<IActionResult> ScrubClaim([FromBody] ScrubRequest request)
        {
            if (request == null || request.ClaimId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "ClaimId is required."
                });
            }

            var results = await _claimScrubService.ScrubClaimAsync(request.ClaimId);
            return Ok(results);
        }

        public sealed class ScrubRequest
        {
            public int ClaimId { get; set; }
        }

        public sealed class SendBatchRequest
        {
            public List<int> ClaimIds { get; set; } = [];
            public bool ForceResubmit { get; set; }
            public string? IdempotencyKey { get; set; }
            public Guid? SubmitterReceiverId { get; set; }
            public string? ConnectionType { get; set; }
            public Guid? ConnectionLibraryId { get; set; }
        }

        public sealed class FailedClaimDto
        {
            public int ClaimId { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public sealed class BlockedClaimDto
        {
            public int ClaimId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public sealed class SendBatchResponseDto
        {
            public bool Success { get; set; }
            public string BatchId { get; set; } = string.Empty;
            public int Total { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public string? FilePath { get; set; }
            public List<FailedClaimDto> FailedClaims { get; set; } = [];
            public List<BlockedClaimDto> BlockedClaims { get; set; } = [];
        }

        public sealed class ClaimBatchListItemDto
        {
            public Guid Id { get; set; }
            public string Status { get; set; } = string.Empty;
            public int SubmissionNumber { get; set; }
            public Guid? SubmitterReceiverId { get; set; }
            public string? ConnectionType { get; set; }
            public int TotalClaims { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public string? FilePath { get; set; }
        }

        public sealed class ClaimBatchItemDto
        {
            public int Id { get; set; }
            public int ClaimId { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? ErrorMessage { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public sealed class ClaimBatchDetailDto
        {
            public Guid Id { get; set; }
            public string Status { get; set; } = string.Empty;
            public int SubmissionNumber { get; set; }
            public Guid? SubmitterReceiverId { get; set; }
            public string? ConnectionType { get; set; }
            public int TotalClaims { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public string? FilePath { get; set; }
            public List<ClaimBatchItemDto> Items { get; set; } = [];
        }

        /// <summary>
        /// Evaluate claim for secondary: rule-driven PR/CO forwardable amount, create secondary if eligible.
        /// Call after ERA is posted and reconciliation passes, or after manual posting.
        /// </summary>
        [HttpPost("{id:int}/evaluate-secondary")]
        public async Task<IActionResult> EvaluateSecondary(int id)
        {
            var result = await _secondaryTriggerService.EvaluateAndTriggerAsync(id);
            return Ok(new
            {
                triggered = result.Triggered,
                reason = result.Reason,
                forwardAmount = result.ForwardAmount,
                secondaryClaimId = result.SecondaryClaimId
            });
        }

        [HttpGet("sendable")]
        public async Task<IActionResult> GetSendableClaims([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
        {
            if (page < 1)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page must be at least 1"
                });
            }

            if (pageSize < 1 || pageSize > 500)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page size must be between 1 and 500"
                });
            }

            var query = await BuildSendableClaimsQueryAsync(cancellationToken);
            var totalCount = await query.CountAsync();
            var rows = await query
                .OrderByDescending(c => c.ClaID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    claID = c.ClaID,
                    claStatus = c.ClaStatus,
                    claSubmissionMethod = c.ClaSubmissionMethod,
                    claDateTimeCreated = c.ClaDateTimeCreated,
                    claTotalChargeTRIG = c.ClaTotalChargeTRIG,
                    claTotalBalanceCC = c.ClaTotalBalanceCC,
                    claPatFID = c.ClaPatFID,
                    // TEMP DEBUG: expose payer mapping used by Send Claims eligibility.
                    debugPayer = c.Claim_Insureds
                        .Where(i => i.ClaInsSequence == 1)
                        .Select(i => new
                        {
                            claInsPayFID = (int?)i.ClaInsPayFID,
                            payID = (int?)i.ClaInsPayF.PayID,
                            payName = i.ClaInsPayF.PayName,
                            payExternalID = i.ClaInsPayF.PayExternalID
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                _logger.LogInformation(
                    "Claim {id} -> Status={status}, PayFID={payId}, PayerPayID={payerPayId}, Name={name}, ExternalID={extId}",
                    row.claID,
                    row.claStatus,
                    row.debugPayer?.claInsPayFID,
                    row.debugPayer?.payID,
                    row.debugPayer?.payName,
                    row.debugPayer?.payExternalID);
            }

            return Ok(new
            {
                data = rows,
                meta = new { page, pageSize, totalCount }
            });
        }

        [HttpPost("send-batch")]
        public async Task<IActionResult> SendBatch([FromBody] SendBatchRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "SendBatch start. Tenant={TenantId}, Facility={FacilityId}, Request={@Request}",
                _currentContext.TenantId,
                _currentContext.FacilityId,
                request);

            if (request?.ClaimIds == null || request.ClaimIds.Count == 0)
            {
                _logger.LogError("SendBatch validation failed: missing claimIds. Request={@Request}", request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "At least one claimId is required."
                });
            }

            var claimIds = request.ClaimIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (claimIds.Count == 0)
            {
                _logger.LogError("SendBatch validation failed: no positive claimIds. Request={@Request}", request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Claim IDs must be positive integers."
                });
            }

            if (!request.SubmitterReceiverId.HasValue)
            {
                _logger.LogError("SendBatch validation failed: SubmitterReceiverId missing. Request={@Request}", request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "SubmitterReceiverId is required."
                });
            }

            var connectionTrimmed = request.ConnectionType?.Trim();
            if (string.IsNullOrEmpty(connectionTrimmed) ||
                (!string.Equals(connectionTrimmed, "Export", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(connectionTrimmed, "Clearinghouse", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError(
                    "SendBatch validation failed: invalid ConnectionType='{ConnectionType}'. Request={@Request}",
                    request.ConnectionType,
                    request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "ConnectionType must be Export or Clearinghouse."
                });
            }

            if (string.Equals(connectionTrimmed, "Clearinghouse", StringComparison.OrdinalIgnoreCase) &&
                (!request.ConnectionLibraryId.HasValue || request.ConnectionLibraryId.Value == Guid.Empty))
            {
                _logger.LogError("SendBatch validation failed: ConnectionLibraryId missing for clearinghouse. Request={@Request}", request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "ConnectionLibraryId is required for Clearinghouse."
                });
            }

            try
            {
                _logger.LogInformation("SendBatch creating batch. ClaimCount={ClaimCount}", claimIds.Count);
                var creation = await _claimBatchService.CreateBatchAsync(
                    new CreateBatchRequest
                    {
                        ClaimIds = claimIds,
                        ForceResubmit = request.ForceResubmit,
                        IdempotencyKey = request.IdempotencyKey,
                        SubmitterReceiverId = request.SubmitterReceiverId,
                        ConnectionType = request.ConnectionType,
                        ConnectionLibraryId = request.ConnectionLibraryId,
                        TenantId = _currentContext.TenantId,
                        FacilityId = _currentContext.FacilityId,
                        CreatedBy = _userContext.UserName
                    },
                    cancellationToken);
                _logger.LogInformation(
                    "Batch created. BatchId={BatchId}, IsIdempotentHit={IsIdempotentHit}, BlockedClaims={BlockedCount}",
                    creation.BatchId,
                    creation.IsIdempotentHit,
                    creation.BlockedClaims.Count);

                if (creation.BlockedClaims.Count == claimIds.Count)
                {
                    _logger.LogWarning(
                        "SendBatch: all selected claims were blocked. BatchId={BatchId}, Selected={SelectedCount}",
                        creation.BatchId,
                        claimIds.Count);
                    return Conflict(new ErrorResponseDto
                    {
                        ErrorCode = "BATCH_CONFLICT",
                        Message = "No eligible claims were available to send for this batch."
                    });
                }

                if (creation.IsIdempotentHit)
                {
                    var existing = await _claimBatchService.GetBatchAsync(
                        new GetBatchRequest
                        {
                            BatchId = creation.BatchId,
                            TenantId = _currentContext.TenantId,
                            FacilityId = _currentContext.FacilityId
                        },
                        cancellationToken);

                    if (existing != null)
                    {
                        return Ok(new SendBatchResponseDto
                        {
                            Success = existing.SuccessCount > 0,
                            BatchId = existing.Id.ToString(),
                            Total = claimIds.Count,
                            SuccessCount = existing.SuccessCount,
                            FailureCount = existing.FailureCount,
                            FilePath = existing.FilePath,
                            FailedClaims = existing.Items
                                .Where(i => i.Status == "Failed" && !string.IsNullOrWhiteSpace(i.ErrorMessage))
                                .Select(i => new FailedClaimDto { ClaimId = i.ClaimId, ErrorMessage = i.ErrorMessage! })
                                .ToList(),
                            BlockedClaims = creation.BlockedClaims
                                .Select(b => new BlockedClaimDto { ClaimId = b.ClaimId, Reason = b.Reason })
                                .ToList()
                        });
                    }
                }

                _logger.LogInformation("Batch processing started. BatchId={BatchId}", creation.BatchId);
                var processResult = await _claimBatchService.ProcessBatchAsync(
                    new ProcessBatchRequest
                    {
                        BatchId = creation.BatchId,
                        TenantId = _currentContext.TenantId,
                        FacilityId = _currentContext.FacilityId
                    },
                    cancellationToken);
                _logger.LogInformation(
                    "Batch completed. BatchId={BatchId}, Success={SuccessCount}, Failed={FailureCount}",
                    processResult.BatchId,
                    processResult.SuccessCount,
                    processResult.FailureCount);

                var batchRows = await _db.ClaimBatches
                    .AsNoTracking()
                    .Where(b => b.Id == processResult.BatchId)
                    .Select(b => new
                    {
                        b.Id,
                        b.Status,
                        b.FilePath,
                        b.CreatedAt,
                        b.SubmittedAt
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                var batchItemCount = await _db.ClaimBatchItems
                    .AsNoTracking()
                    .CountAsync(i => i.BatchId == processResult.BatchId, cancellationToken);

                _logger.LogInformation(
                    "Batch persistence check. Batch={@Batch}, BatchItemCount={BatchItemCount}, FileGenerated={FileGenerated}",
                    batchRows,
                    batchItemCount,
                    !string.IsNullOrWhiteSpace(batchRows?.FilePath));

                return Ok(new SendBatchResponseDto
                {
                    Success = processResult.SuccessCount > 0,
                    BatchId = processResult.BatchId.ToString(),
                    Total = claimIds.Count,
                    SuccessCount = processResult.SuccessCount,
                    FailureCount = processResult.FailureCount,
                    FilePath = batchRows?.FilePath,
                    FailedClaims = processResult.FailedClaims
                        .Select(f => new FailedClaimDto { ClaimId = f.ClaimId, ErrorMessage = f.ErrorMessage })
                        .ToList(),
                    BlockedClaims = creation.BlockedClaims
                        .Select(b => new BlockedClaimDto { ClaimId = b.ClaimId, Reason = b.Reason })
                        .ToList()
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "SendBatch failed with argument error. Request={@Request}", request);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "SendBatch failed with operation conflict. Request={@Request}", request);
                return Conflict(new ErrorResponseDto
                {
                    ErrorCode = "BATCH_CONFLICT",
                    Message = ex.Message
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "SendBatch failed with DB concurrency error. Request={@Request}", request);
                return Conflict(new ErrorResponseDto
                {
                    ErrorCode = "BATCH_CONCURRENCY",
                    Message = "Batch was modified by another process. Please retry."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendBatch failed with unexpected error. Request={@Request}", request);
                throw;
            }
        }

        [HttpPost("retry-batch/{batchId:guid}")]
        public async Task<IActionResult> RetryBatch([FromRoute] Guid batchId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _claimBatchService.RetryBatchAsync(
                    new RetryBatchRequest
                    {
                        BatchId = batchId,
                        TenantId = _currentContext.TenantId,
                        FacilityId = _currentContext.FacilityId
                    },
                    cancellationToken);

                return Ok(new SendBatchResponseDto
                {
                    BatchId = result.BatchId.ToString(),
                    Total = result.TotalClaims,
                    SuccessCount = result.SuccessCount,
                    FailureCount = result.FailureCount,
                    FailedClaims = result.FailedClaims
                        .Select(f => new FailedClaimDto { ClaimId = f.ClaimId, ErrorMessage = f.ErrorMessage })
                        .ToList()
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Batch not found." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto { ErrorCode = "BATCH_CONFLICT", Message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new ErrorResponseDto
                {
                    ErrorCode = "BATCH_CONCURRENCY",
                    Message = "Batch was modified by another process. Please retry."
                });
            }
        }

        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1 || pageSize < 1 || pageSize > 200)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values."
                });
            }

            var result = await _claimBatchService.GetBatchesAsync(
                new GetBatchesRequest
                {
                    TenantId = _currentContext.TenantId,
                    FacilityId = _currentContext.FacilityId,
                    Page = page,
                    PageSize = pageSize
                },
                CancellationToken.None);

            var data = result.Items.Select(b => new ClaimBatchListItemDto
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
            }).ToList();

            return Ok(new
            {
                data,
                meta = new { page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount }
            });
        }

        [HttpGet("batches/{batchId:guid}")]
        public async Task<IActionResult> GetBatchById([FromRoute] Guid batchId, CancellationToken cancellationToken)
        {
            var batch = await _claimBatchService.GetBatchAsync(
                new GetBatchRequest
                {
                    BatchId = batchId,
                    TenantId = _currentContext.TenantId,
                    FacilityId = _currentContext.FacilityId
                },
                cancellationToken);
            if (batch == null)
            {
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Batch not found." });
            }

            return Ok(new ClaimBatchDetailDto
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
                Items = batch.Items.Select(i => new ClaimBatchItemDto
                {
                    Id = i.Id,
                    ClaimId = i.ClaimId,
                    Status = i.Status,
                    ErrorMessage = i.ErrorMessage,
                    CreatedAt = i.CreatedAt
                }).ToList()
            });
        }

        [HttpGet("batches/{batchId:guid}/edi")]
        public async Task<IActionResult> GetBatchEdi([FromRoute] Guid batchId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _claimBatchService.GetBatchEdiAsync(
                    new GetBatchRequest
                    {
                        BatchId = batchId,
                        TenantId = _currentContext.TenantId,
                        FacilityId = _currentContext.FacilityId
                    },
                    cancellationToken);

                return Ok(new
                {
                    batchId = result.BatchId,
                    ediContent = result.EdiContent
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Batch not found." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = ex.Message });
            }
        }

        [HttpPost("batches/{batchId:guid}/export-zip")]
        public async Task<IActionResult> ExportBatchZip([FromRoute] Guid batchId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _claimBatchService.ExportBatchZipAsync(
                    new GetBatchRequest
                    {
                        BatchId = batchId,
                        TenantId = _currentContext.TenantId,
                        FacilityId = _currentContext.FacilityId
                    },
                    cancellationToken);

                return File(result.Content, "application/zip", result.FileName);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Batch not found." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = ex.Message });
            }
        }

        private async Task<IQueryable<Claim>> BuildSendableClaimsQueryAsync(CancellationToken cancellationToken)
        {
            var tenantId = _currentContext.TenantId;
            var facilityId = _currentContext.FacilityId;
            var rts = ClaimStatusCatalog.ToStorage(ClaimStatus.RTS);
            var settings = await _sendingClaimsSettingsService.GetSettingsAsync(tenantId, facilityId, cancellationToken);

            return _db.Claims
                .AsNoTracking()
                .WhereEligibleForSend(tenantId, facilityId, rts, settings.ShowBillToPatientClaims);
        }

        [HttpGet]
        public async Task<IActionResult> GetClaims(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? status = null,
            [FromQuery] string? statusList = null, // Comma-separated list of statuses (Excel-style)
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? searchText = null, // Text search across multiple columns
            [FromQuery] int? minClaimId = null,
            [FromQuery] int? maxClaimId = null,
            [FromQuery] decimal? minTotalCharge = null,
            [FromQuery] decimal? maxTotalCharge = null,
            [FromQuery] decimal? minTotalBalance = null,
            [FromQuery] decimal? maxTotalBalance = null,
            [FromQuery] int? patientId = null, // Filter by patient (ClaPatFID)
            [FromQuery] string? patAccountNo = null, // Filter by patient account number (exact match; from Account # column filter)
            [FromQuery] string? additionalColumns = null) // Comma-separated list of additional column keys to include
        {
            // Validate input
            if (page < 1)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page must be at least 1"
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page size must be between 1 and 100"
                });
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "From date must be before or equal to to date"
                });
            }

            // Parse additional columns
            var requestedColumns = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                requestedColumns = additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();
            }

            // Use requested keys directly so Claim List Add Column is not constrained by a stale server whitelist.
            var columnsToInclude = requestedColumns
                .Select(k => new RelatedColumnDefinition { Key = k, Label = k, Table = "Claim", Path = k })
                .ToList();
            
            // Pre-evaluate which columns are requested to avoid evaluating in Select()
            var hasPatFirstName = columnsToInclude.Any(col => col.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(col => col.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(col => col.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(col => col.Key == "patAccountNo");
            var hasPatPhoneNo = columnsToInclude.Any(col => col.Key == "patPhoneNo");
            var hasPatCity = columnsToInclude.Any(col => col.Key == "patCity");
            var hasPatState = columnsToInclude.Any(col => col.Key == "patState");
            var hasPatBirthDate = columnsToInclude.Any(col => col.Key == "patBirthDate");
            var hasPatDob = columnsToInclude.Any(col => col.Key == "patDOB");
            var hasPatClassification = columnsToInclude.Any(col => col.Key == "patClassification");
            var hasPrimaryPayerName = columnsToInclude.Any(col => col.Key == "primaryPayerName");
            var hasAttendingPhysicianName = columnsToInclude.Any(col => col.Key == "attendingPhysicianName");
            var hasReferringPhysicianName = columnsToInclude.Any(col => col.Key == "referringPhysicianName");
            var hasRenderingPhysicianName = columnsToInclude.Any(col => col.Key == "renderingPhysicianName");
            var hasOperatingPhysicianName = columnsToInclude.Any(col => col.Key == "operatingPhysicianName");
            var hasOrderingPhysicianName = columnsToInclude.Any(col => col.Key == "orderingPhysicianName");
            var hasBillingPhysicianName = columnsToInclude.Any(col => col.Key == "billingPhysicianName");
            var hasSupervisingPhysicianName = columnsToInclude.Any(col => col.Key == "supervisingPhysicianName");
            var hasSecondaryPayerName = columnsToInclude.Any(col => col.Key == "secondaryPayerName");
            var hasPrimaryPayerID = columnsToInclude.Any(col => col.Key == "primaryPayerID");
            var hasPrimaryPayerPhone = columnsToInclude.Any(col => col.Key == "primaryPayerPhone");
            var hasPriInsClaimFilingInd = columnsToInclude.Any(col => col.Key == "priInsClaimFilingInd");
            var hasSecInsClaimFilingInd = columnsToInclude.Any(col => col.Key == "secInsClaimFilingInd");
            var hasPrimaryInsuredID = columnsToInclude.Any(col => col.Key == "primaryInsuredID");
            var hasPrimaryInsuredName = columnsToInclude.Any(col => col.Key == "primaryInsuredName");
            var hasPrimaryInsuredDOB = columnsToInclude.Any(col => col.Key == "primaryInsuredDOB");
            var hasPrimaryInsuredEmployer = columnsToInclude.Any(col => col.Key == "primaryInsuredEmployer");
            var hasPrimaryInsuredPlan = columnsToInclude.Any(col => col.Key == "primaryInsuredPlan");
            var hasRenderingPhyName = columnsToInclude.Any(col => col.Key == "renderingPhyName");
            var hasRenderingPhyNPI = columnsToInclude.Any(col => col.Key == "renderingPhyNPI");
            var hasBillingPhyName = columnsToInclude.Any(col => col.Key == "billingPhyName");
            var hasBillingPhyNPI = columnsToInclude.Any(col => col.Key == "billingPhyNPI");
            var hasFacilityName = columnsToInclude.Any(col => col.Key == "facilityName");

            // Build efficient LINQ query with server-side filtering
            var tenantId = _currentContext.TenantId;
            var facilityId = _currentContext.FacilityId;
            var scopedServiceLines = _db.Service_Lines
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.FacilityId == facilityId && s.SrvClaFID.HasValue);
            var serviceLineAgg = scopedServiceLines
                .GroupBy(s => s.SrvClaFID!.Value)
                .Select(g => new
                {
                    ClaimId = g.Key,
                    TotalCharge = g.Sum(x => (decimal?)x.SrvCharges) ?? 0m,
                    TotalInsBalance = g.Sum(x => x.SrvTotalInsBalanceCC) ?? 0m,
                    TotalPatBalance = g.Sum(x => x.SrvTotalPatBalanceCC) ?? 0m,
                    TotalBalance = g.Sum(x => x.SrvTotalBalanceCC) ?? 0m
                });

            var baseQuery = _db.Claims.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.FacilityId == facilityId);

            // Excel-style status filter: support both single status and comma-separated list
            if (!string.IsNullOrWhiteSpace(statusList))
            {
                var statuses = statusList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (statuses.Any())
                {
                    baseQuery = baseQuery.Where(c => c.ClaStatus != null && statuses.Contains(c.ClaStatus));
                }
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(c => c.ClaStatus == status);
            }

            // Date range filter
            if (fromDate.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.ClaDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.ClaDateTimeCreated <= toDate.Value);
            }

            // Claim ID range filter
            if (minClaimId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.ClaID >= minClaimId.Value);
            }

            if (maxClaimId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.ClaID <= maxClaimId.Value);
            }

            var query = from c in baseQuery
                        join agg in serviceLineAgg on c.ClaID equals agg.ClaimId into aggJoin
                        from agg in aggJoin.DefaultIfEmpty()
                        select new { Claim = c, Agg = agg };

            // Total Charge range filter
            if (minTotalCharge.HasValue)
            {
                query = query.Where(x => (x.Agg != null ? x.Agg.TotalCharge : 0m) >= minTotalCharge.Value);
            }

            if (maxTotalCharge.HasValue)
            {
                query = query.Where(x => (x.Agg != null ? x.Agg.TotalCharge : 0m) <= maxTotalCharge.Value);
            }

            // Total Balance range filter
            if (minTotalBalance.HasValue)
            {
                query = query.Where(x => (x.Agg != null ? x.Agg.TotalBalance : 0m) >= minTotalBalance.Value);
            }

            if (maxTotalBalance.HasValue)
            {
                query = query.Where(x => (x.Agg != null ? x.Agg.TotalBalance : 0m) <= maxTotalBalance.Value);
            }

            // Patient filter (for ribbon: open claims for a specific patient)
            if (patientId.HasValue)
            {
                query = query.Where(x => x.Claim.ClaPatFID == patientId.Value);
            }

            // Patient account number filter (from Claim List Account # column filter – exact match)
            if (!string.IsNullOrWhiteSpace(patAccountNo))
            {
                var accountNoTrimmed = patAccountNo.Trim();
                query = query.Where(x => x.Claim.ClaPatF != null && x.Claim.ClaPatF.PatAccountNo != null && x.Claim.ClaPatF.PatAccountNo == accountNoTrimmed);
            }

            // Text search across multiple columns (optimized for SQL)
            // Note: Avoid ToString().Contains() as it's very slow - use direct comparisons instead
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower().Trim();
                
                // Try to parse as number for ID and numeric fields (much faster)
                if (int.TryParse(searchText, out int searchInt))
                {
                    query = query.Where(x => x.Claim.ClaID == searchInt);
                }
                else if (decimal.TryParse(searchText, out decimal searchDecimal))
                {
                    query = query.Where(x =>
                        (x.Agg != null ? x.Agg.TotalCharge : 0m) == searchDecimal ||
                        (x.Claim.ClaTotalAmtPaidCC.HasValue && x.Claim.ClaTotalAmtPaidCC.Value == searchDecimal) ||
                        (x.Agg != null ? x.Agg.TotalBalance : 0m) == searchDecimal);
                }
                else
                {
                    // Text search only on string fields (can use indexes if available)
                    query = query.Where(x => x.Claim.ClaStatus != null && x.Claim.ClaStatus.ToLower().Contains(searchLower));
                }
            }

            // Order by ID descending for consistent pagination
            query = query.OrderByDescending(x => x.Claim.ClaID);

            var countQuery = query.Select(x => x.Claim.ClaID);
            var pagedQuery = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    Claim = x.Claim,
                    TotalCharge = x.Agg != null ? x.Agg.TotalCharge : 0m,
                    TotalInsBalance = x.Agg != null ? x.Agg.TotalInsBalance : 0m,
                    TotalPatBalance = x.Agg != null ? x.Agg.TotalPatBalance : 0m,
                    TotalBalance = x.Agg != null ? x.Agg.TotalBalance : 0m
                });

            int totalCount;
            try
            {
                // Count from filtered base query (no correlated projection).
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var countSw = Stopwatch.StartNew();
                totalCount = await countQuery.CountAsync(cts.Token);
                countSw.Stop();
                _logger.LogInformation("FindClaims count query duration={DurationMs}ms", countSw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("FindClaims count query timed out for tenant={tenantId}, facility={facilityId}", tenantId, facilityId);
                return StatusCode(503, new ErrorResponseDto
                {
                    ErrorCode = "QUERY_TIMEOUT",
                    Message = "The claim count query took too long. Please narrow your filters and try again."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FindClaims count query failed for tenant={tenantId}, facility={facilityId}", tenantId, facilityId);
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "INTERNAL_ERROR",
                    Message = "Failed to count claims."
                });
            }

            _logger.LogInformation("FindClaims: tenant={tenantId}, facility={facilityId}, total={count}", tenantId, facilityId, totalCount);

            _logger.LogInformation("FindClaims count SQL: {Sql}", countQuery.ToQueryString());
            _logger.LogInformation("FindClaims data SQL: {Sql}", pagedQuery.ToQueryString());
            var dataSw = Stopwatch.StartNew();
            var pageRows = await pagedQuery.ToListAsync();
            dataSw.Stop();
            _logger.LogInformation("FindClaims data query duration={DurationMs}ms", dataSw.ElapsedMilliseconds);

            var claimIds = pageRows.Select(r => r.Claim.ClaID).ToList();
            var patientIds = pageRows.Select(r => r.Claim.ClaPatFID).Distinct().ToList();
            var physicianIds = pageRows
                .SelectMany(r => new[]
                {
                    r.Claim.ClaAttendingPhyFID, r.Claim.ClaReferringPhyFID, r.Claim.ClaRenderingPhyFID, r.Claim.ClaOperatingPhyFID,
                    r.Claim.ClaOrderingPhyFID, r.Claim.ClaBillingPhyFID, r.Claim.ClaSupervisingPhyFID, r.Claim.ClaFacilityPhyFID
                })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var patients = await _db.Patients.AsNoTracking()
                .Where(p => patientIds.Contains(p.PatID))
                .ToDictionaryAsync(p => p.PatID);

            var physicians = await _db.Physicians.AsNoTracking()
                .Where(p => physicianIds.Contains(p.PhyID))
                .ToDictionaryAsync(p => p.PhyID);

            var claimInsuredRows = await _db.Claim_Insureds.AsNoTracking()
                .Where(ci => claimIds.Contains(ci.ClaInsClaFID) && (ci.ClaInsSequence == 1 || ci.ClaInsSequence == 2))
                .Select(ci => new
                {
                    ci.ClaInsClaFID,
                    ci.ClaInsSequence,
                    PayName = ci.ClaInsPayF != null ? ci.ClaInsPayF.PayName : null,
                    PayExternalId = ci.ClaInsPayF != null ? ci.ClaInsPayF.PayExternalID : null,
                    PayPhone = ci.ClaInsPayF != null ? ci.ClaInsPayF.PayPhoneNo : null,
                    ClaimFilingIndicator = ci.ClaInsClaimFilingIndicator,
                    ci.ClaInsIDNumber,
                    ci.ClaInsFirstName,
                    ci.ClaInsLastName,
                    ci.ClaInsBirthDate,
                    ci.ClaInsEmployer,
                    ci.ClaInsPlanName
                })
                .ToListAsync();
            var claimInsuredMap = claimInsuredRows
                .GroupBy(x => (x.ClaInsClaFID, x.ClaInsSequence))
                .ToDictionary(g => g.Key, g => g.First());

            var patientInsuredRows = await _db.Patient_Insureds.AsNoTracking()
                .Where(pi => patientIds.Contains(pi.PatInsPatFID) && (pi.PatInsSequence == 1 || pi.PatInsSequence == 2))
                .Select(pi => new
                {
                    pi.PatInsPatFID,
                    pi.PatInsSequence,
                    PayName = pi.PatInsIns != null && pi.PatInsIns.InsPay != null ? pi.PatInsIns.InsPay.PayName : null,
                    PayExternalId = pi.PatInsIns != null && pi.PatInsIns.InsPay != null ? pi.PatInsIns.InsPay.PayExternalID : null,
                    PayPhone = pi.PatInsIns != null && pi.PatInsIns.InsPay != null ? pi.PatInsIns.InsPay.PayPhoneNo : null,
                    ClaimFilingIndicator = pi.PatInsIns != null ? pi.PatInsIns.InsClaimFilingIndicator : null,
                    InsIdNumber = pi.PatInsIns != null ? pi.PatInsIns.InsIDNumber : null,
                    InsFirstName = pi.PatInsIns != null ? pi.PatInsIns.InsFirstName : null,
                    InsLastName = pi.PatInsIns != null ? pi.PatInsIns.InsLastName : null,
                    InsBirthDate = pi.PatInsIns != null ? pi.PatInsIns.InsBirthDate : (DateOnly?)null,
                    InsEmployer = pi.PatInsIns != null ? pi.PatInsIns.InsEmployer : null,
                    InsPlanName = pi.PatInsIns != null ? pi.PatInsIns.InsPlanName : null
                })
                .ToListAsync();
            var patientInsuredMap = patientInsuredRows
                .GroupBy(x => (x.PatInsPatFID, x.PatInsSequence))
                .ToDictionary(g => g.Key, g => g.First());

            var auditRows = await _db.Claim_Audits.AsNoTracking()
                .Where(a => claimIds.Contains(a.ClaFID))
                .Select(a => new { a.ClaFID, a.ActivityDate, a.UserName })
                .ToListAsync();
            var firstAuditUser = auditRows
                .GroupBy(a => a.ClaFID)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.ActivityDate).Select(x => x.UserName).FirstOrDefault());
            var lastAuditUser = auditRows
                .GroupBy(a => a.ClaFID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ActivityDate).Select(x => x.UserName).FirstOrDefault());

            string? GetPhysicianName(int id) => id > 0 && physicians.TryGetValue(id, out var px) ? px.PhyName : null;
            string? GetPhysicianNpi(int id) => id > 0 && physicians.TryGetValue(id, out var px) ? px.PhyNPI : null;
            string? BuildInsuredName(string? lastName, string? firstName) => string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName)
                ? null
                : ((lastName ?? "") + ", " + (firstName ?? "")).Trim().Trim(',');

            var result = pageRows.Select(r =>
            {
                var c = r.Claim;
                patients.TryGetValue(c.ClaPatFID, out var pat);
                claimInsuredMap.TryGetValue((c.ClaID, 1), out var claimPrimary);
                claimInsuredMap.TryGetValue((c.ClaID, 2), out var claimSecondary);
                patientInsuredMap.TryGetValue((c.ClaPatFID, 1), out var patientPrimary);
                patientInsuredMap.TryGetValue((c.ClaPatFID, 2), out var patientSecondary);

                var primaryPayerName = patientPrimary?.PayName ?? claimPrimary?.PayName;
                var secondaryPayerName = patientSecondary?.PayName ?? claimSecondary?.PayName;
                var facilityName = GetPhysicianName(c.ClaFacilityPhyFID);

                var claimDto = new ClaimListItemDto
                {
                    ClaID = c.ClaID,
                    ClaStatus = c.ClaStatus,
                    ClaDateTimeCreated = c.ClaDateTimeCreated,
                    ClaDateTimeModified = c.ClaDateTimeModified,
                    CreatedDate = c.ClaDateTimeCreated,
                    ModifiedDate = c.ClaDateTimeModified,
                    ClaTotalChargeTRIG = r.TotalCharge,
                    ClaTotalInsBalanceTRIG = r.TotalInsBalance,
                    ClaTotalPatBalanceTRIG = r.TotalPatBalance,
                    ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                    ClaTotalBalanceCC = r.TotalBalance,
                    ClaClassification = c.ClaClassification ?? facilityName,
                    ClaDateTotalFrom = c.ClaDateTotalFrom,
                    ClaBillTo = c.ClaBillTo,
                    BillToDisplay = c.ClaBillTo == 1 ? $"P - {claimPrimary?.PayName ?? "PRIMARY"}" :
                                    c.ClaBillTo == 2 ? $"S - {claimSecondary?.PayName ?? "SECONDARY"}" : "Patient",
                    PatFullNameCC = pat?.PatFullNameCC,
                    PrimaryPayerName = primaryPayerName,
                    ClaPatFID = c.ClaPatFID,
                    ClaAttendingPhyFID = c.ClaAttendingPhyFID,
                    ClaBillingPhyFID = c.ClaBillingPhyFID,
                    ClaReferringPhyFID = c.ClaReferringPhyFID,
                    ClaBillDate = c.ClaBillDate,
                    ClaTypeOfBill = c.ClaTypeOfBill,
                    ClaAdmissionType = c.ClaAdmissionType,
                    ClaPatientStatus = c.ClaPatientStatus,
                    ClaCreatedUserName = c.ClaCreatedUserName ?? (firstAuditUser.TryGetValue(c.ClaID, out var createdBy) ? createdBy : null),
                    ClaLastUserName = c.ClaLastUserName ?? (lastAuditUser.TryGetValue(c.ClaID, out var lastBy) ? lastBy : null),
                    ClaDiagnosis1 = c.ClaDiagnosis1,
                    ClaDiagnosis2 = c.ClaDiagnosis2,
                    ClaDiagnosis3 = c.ClaDiagnosis3,
                    ClaDiagnosis4 = c.ClaDiagnosis4,
                    ClaFirstDateTRIG = c.ClaFirstDateTRIG,
                    ClaLastDateTRIG = c.ClaLastDateTRIG
                };

                if (columnsToInclude.Any())
                {
                    claimDto.AdditionalColumns = new Dictionary<string, object?>();
                    foreach (var col in columnsToInclude)
                    {
                        object? value = col.Key switch
                        {
                            "patFirstName" => pat?.PatFirstName,
                            "patLastName" => pat?.PatLastName,
                            "patFullNameCC" => pat?.PatFullNameCC,
                            "primaryPayerName" => primaryPayerName,
                            "patAccountNo" => pat?.PatAccountNo,
                            "patPhoneNo" => pat?.PatPhoneNo,
                            "patCity" => pat?.PatCity,
                            "patState" => pat?.PatState,
                            "patBirthDate" => pat?.PatBirthDate,
                            "patDOB" => pat?.PatBirthDate,
                            "patClassification" => pat?.PatClassification,
                            "patID" => c.ClaPatFID,
                            "claDateTotalFrom" => c.ClaDateTotalFrom,
                            "claLastDateTRIG" => c.ClaLastDateTRIG,
                            "claFirstDOS" => c.ClaFirstDateTRIG,
                            "claLastDOS" => c.ClaLastDateTRIG,
                            "claTotalChargeTRIG" => claimDto.ClaTotalChargeTRIG,
                            "claTotalInsBalanceTRIG" => claimDto.ClaTotalInsBalanceTRIG,
                            "claTotalPatBalanceTRIG" => claimDto.ClaTotalPatBalanceTRIG,
                            "claTotalBalanceCC" => claimDto.ClaTotalBalanceCC,
                            "claTotalCharge" => claimDto.ClaTotalChargeTRIG,
                            "claTotalInsBalance" => claimDto.ClaTotalInsBalanceTRIG,
                            "claTotalPatBalance" => claimDto.ClaTotalPatBalanceTRIG,
                            "claTotalBalance" => claimDto.ClaTotalBalanceCC,
                            "attendingPhysicianName" => GetPhysicianName(c.ClaAttendingPhyFID),
                            "referringPhysicianName" => GetPhysicianName(c.ClaReferringPhyFID),
                            "renderingPhysicianName" => GetPhysicianName(c.ClaRenderingPhyFID),
                            "operatingPhysicianName" => GetPhysicianName(c.ClaOperatingPhyFID),
                            "orderingPhysicianName" => GetPhysicianName(c.ClaOrderingPhyFID),
                            "billingPhysicianName" => GetPhysicianName(c.ClaBillingPhyFID),
                            "supervisingPhysicianName" => GetPhysicianName(c.ClaSupervisingPhyFID),
                            "secondaryPayerName" => secondaryPayerName,
                            "primaryPayerID" => patientPrimary?.PayExternalId ?? claimPrimary?.PayExternalId,
                            "primaryPayerPhone" => patientPrimary?.PayPhone ?? claimPrimary?.PayPhone,
                            "priInsClaimFilingInd" => patientPrimary?.ClaimFilingIndicator ?? claimPrimary?.ClaimFilingIndicator,
                            "secInsClaimFilingInd" => patientSecondary?.ClaimFilingIndicator ?? claimSecondary?.ClaimFilingIndicator,
                            "primaryInsuredID" => patientPrimary?.InsIdNumber ?? claimPrimary?.ClaInsIDNumber,
                            "primaryInsuredName" => BuildInsuredName(patientPrimary?.InsLastName ?? claimPrimary?.ClaInsLastName, patientPrimary?.InsFirstName ?? claimPrimary?.ClaInsFirstName),
                            "primaryInsuredDOB" => patientPrimary?.InsBirthDate ?? claimPrimary?.ClaInsBirthDate,
                            "primaryInsuredEmployer" => patientPrimary?.InsEmployer ?? claimPrimary?.ClaInsEmployer,
                            "primaryInsuredPlan" => patientPrimary?.InsPlanName ?? claimPrimary?.ClaInsPlanName,
                            "renderingPhyName" => GetPhysicianName(c.ClaRenderingPhyFID),
                            "renderingPhyNPI" => GetPhysicianNpi(c.ClaRenderingPhyFID),
                            "billingPhyName" => GetPhysicianName(c.ClaBillingPhyFID),
                            "billingPhyNPI" => GetPhysicianNpi(c.ClaBillingPhyFID),
                            "facilityName" => facilityName,
                            "claClassification" => claimDto.ClaClassification,
                            "claCreatedUser" => claimDto.ClaCreatedUserName,
                            "claModifiedUser" => claimDto.ClaLastUserName,
                            "claCreatedUserName" => claimDto.ClaCreatedUserName,
                            "claLastUserName" => claimDto.ClaLastUserName,
                            _ => null
                        };
                        if (value == null)
                        {
                            value = GetClaimColumnValue(c, col.Key);
                        }
                        claimDto.AdditionalColumns[col.Key] = value;
                    }
                }

                return claimDto;
            }).ToList();

            return Ok(new ApiResponse<List<ClaimListItemDto>>
            {
                Data = result,
                Meta = new PaginationMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            });
        }

        [HttpGet("user-kpis")]
        public async Task<IActionResult> GetUserKpis([FromQuery] int trendDays = 30)
        {
            var userName = _userContext.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Ok(new UserKpiDashboardDto());
            }

            if (trendDays < 7) trendDays = 7;
            if (trendDays > 90) trendDays = 90;

            var nowUtc = DateTime.UtcNow;
            var trendStartUtc = nowUtc.Date.AddDays(-(trendDays - 1));
            // Source of truth for "claims edited" = Claim_Audit (same table used by Find Claim Note).
            var normalizedUserName = userName.ToLower();
            var userEditAudits = _db.Claim_Audits
                .AsNoTracking()
                .Where(a => a.ClaFID > 0)
                .Where(a => a.UserName != null && a.UserName.ToLower() == normalizedUserName)
                .Where(a =>
                    a.ActivityType != null &&
                    (EF.Functions.Like(a.ActivityType, "%Claim Edited%") || EF.Functions.Like(a.ActivityType, "%Edit%")));

            var editedClaimIds = userEditAudits
                .Select(a => a.ClaFID)
                .Distinct();
            var userClaims = _db.Claims
                .AsNoTracking()
                .Where(c =>
                    editedClaimIds.Contains(c.ClaID) &&
                    c.TenantId == _currentContext.TenantId &&
                    c.FacilityId == _currentContext.FacilityId);

            var totalClaims = await userClaims.CountAsync();
            if (totalClaims == 0)
            {
                return Ok(new UserKpiDashboardDto
                {
                    UserName = userName
                });
            }

            var totalPaid = await userClaims.SumAsync(c => c.ClaTotalAmtPaidCC ?? 0m);
            var claimIds = userClaims.Select(c => c.ClaID);
            var financialSums = await _db.Service_Lines
                .Where(s => s.SrvClaFID.HasValue && claimIds.Contains(s.SrvClaFID.Value))
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalCharge = g.Sum(x => (decimal?)x.SrvCharges) ?? 0m,
                    TotalBalance = g.Sum(x => x.SrvTotalBalanceCC) ?? 0m
                })
                .FirstOrDefaultAsync();

            var statusData = await userClaims
                .GroupBy(c => string.IsNullOrWhiteSpace(c.ClaStatus) ? "Unknown" : c.ClaStatus!)
                .Select(g => new UserKpiStatusPointDto
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync();

            var trendRows = await userEditAudits
                .Where(a => a.ActivityDate >= trendStartUtc)
                .GroupBy(a => a.ActivityDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
            var trendMap = trendRows.ToDictionary(x => x.Date, x => x.Count);
            var trendData = new List<UserKpiTrendPointDto>(trendDays);
            for (var i = 0; i < trendDays; i++)
            {
                var day = trendStartUtc.AddDays(i).Date;
                trendData.Add(new UserKpiTrendPointDto
                {
                    Label = day.ToString("MMM dd"),
                    Value = trendMap.TryGetValue(day, out var count) ? count : 0
                });
            }

            var claimBalances = await (
                from c in userClaims
                join s in _db.Service_Lines.AsNoTracking() on c.ClaID equals s.SrvClaFID into sg
                select new
                {
                    c.ClaDateTimeCreated,
                    Balance = sg.Sum(x => x.SrvTotalBalanceCC) ?? 0m
                })
                .ToListAsync();

            decimal bucket0To30 = 0m;
            decimal bucket31To60 = 0m;
            decimal bucket61To90 = 0m;
            decimal bucket90Plus = 0m;
            foreach (var row in claimBalances)
            {
                if (row.Balance <= 0m) continue;
                var ageDays = (int)(nowUtc.Date - row.ClaDateTimeCreated.Date).TotalDays;
                if (ageDays <= 30) bucket0To30 += row.Balance;
                else if (ageDays <= 60) bucket31To60 += row.Balance;
                else if (ageDays <= 90) bucket61To90 += row.Balance;
                else bucket90Plus += row.Balance;
            }
            var agingData = new List<UserKpiAgingBucketDto>
            {
                new() { Label = "0-30", Value = bucket0To30 },
                new() { Label = "31-60", Value = bucket31To60 },
                new() { Label = "61-90", Value = bucket61To90 },
                new() { Label = "90+", Value = bucket90Plus }
            };

            var topPayers = await (
                from c in userClaims
                let payerName = _db.Patient_Insureds
                        .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                        .Select(pi => pi.PatInsIns.InsPay.PayName)
                        .FirstOrDefault()
                    ?? _db.Claim_Insureds
                        .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                        .Select(ci => ci.ClaInsPayF.PayName)
                        .FirstOrDefault()
                    ?? "Unknown"
                group c by payerName into g
                orderby g.Count() descending
                select new UserKpiPayerPointDto
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .Take(5)
                .ToListAsync();

            return Ok(new UserKpiDashboardDto
            {
                UserName = userName,
                TotalClaims = totalClaims,
                TotalCharge = financialSums?.TotalCharge ?? 0m,
                TotalPaid = totalPaid,
                TotalBalance = financialSums?.TotalBalance ?? 0m,
                ClaimsByStatus = statusData,
                ClaimsTrend = trendData,
                AgingBuckets = agingData,
                TopPayers = topPayers
            });
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Claim"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        private static object? GetClaimColumnValue(Claim claimEntity, string key)
        {
            // Common UI aliases from Add Column registry to Claim entity members.
            var claimKey = key switch
            {
                "claFirstDOS" => "ClaFirstDateTRIG",
                "claLastDOS" => "ClaLastDateTRIG",
                "claPaidDate" => "ClaPaidDateTRIG",
                "claDischargeDate" => "ClaDischargedDate",
                "claDischargeHour" => "ClaDischargedHour",
                "claLastExported" => "ClaLastExportedDate",
                "claLastPrinted" => "ClaLastPrintedDate",
                "claCreatedTimestamp" => "ClaDateTimeCreated",
                "claModifiedTimestamp" => "ClaDateTimeModified",
                "claCreatedUser" => "ClaCreatedUserName",
                "claModifiedUser" => "ClaLastUserName",
                "claTotalInsAmtPaid" => "ClaTotalInsAmtPaidTRIG",
                "claTotalPatAmtPaid" => "ClaTotalPatAmtPaidTRIG",
                "claTotalCharge" => "ClaTotalChargeTRIG",
                "claTotalInsBalance" => "ClaTotalInsBalanceTRIG",
                "claTotalPatBalance" => "ClaTotalPatBalanceTRIG",
                "claVisitNumber" => "ClaMedicalRecordNumber",
                _ => key
            };

            if (string.Equals(key, "claActive", StringComparison.OrdinalIgnoreCase))
            {
                return !(claimEntity.ClaArchived ?? false);
            }

            var pascal = char.ToUpperInvariant(claimKey[0]) + claimKey.Substring(1);
            var prop = typeof(Claim).GetProperty(
                pascal,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);
            return prop?.GetValue(claimEntity);
        }

        /// <summary>
        /// Get claim notes from Claim_Audit (one row per note). Same data source as Claim Details Notes.
        /// Returns note fields + all claim list columns (claim + patient + additionalColumns).
        /// </summary>
        [HttpGet("notes")]
        public async Task<IActionResult> GetClaimNotes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] int? minClaimId = null,
            [FromQuery] int? maxClaimId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? searchText = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Page must be >= 1 and pageSize 1-100." });
            }

            var requestedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                foreach (var k in additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = k.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) requestedColumns.Add(trimmed);
                }
            }

            var columnsToInclude = requestedColumns
                .Select(k => new RelatedColumnDefinition { Key = k, Label = k, Table = "Claim", Path = k })
                .ToList();
            var hasPatFirstName = columnsToInclude.Any(c => c.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(c => c.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(c => c.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(c => c.Key == "patAccountNo");
            var hasPatPhoneNo = columnsToInclude.Any(c => c.Key == "patPhoneNo");
            var hasPatCity = columnsToInclude.Any(c => c.Key == "patCity");
            var hasPatState = columnsToInclude.Any(c => c.Key == "patState");
            var hasPatBirthDate = columnsToInclude.Any(c => c.Key == "patBirthDate");
            var hasRenderingPhyName = columnsToInclude.Any(c => c.Key == "renderingPhyName");
            var hasRenderingPhyNPI = columnsToInclude.Any(c => c.Key == "renderingPhyNPI");
            var hasBillingPhyName = columnsToInclude.Any(c => c.Key == "billingPhyName");
            var hasBillingPhyNPI = columnsToInclude.Any(c => c.Key == "billingPhyNPI");
            var hasFacilityName = columnsToInclude.Any(c => c.Key == "facilityName");
            var hasCreatedUser = columnsToInclude.Any(c => c.Key == "claCreatedUser" || c.Key == "claCreatedUserName");
            var hasModifiedUser = columnsToInclude.Any(c => c.Key == "claModifiedUser" || c.Key == "claLastUserName");

            try
            {
                // Use LEFT JOINs for Patient and Physicians so claims with FK=0 or missing refs still appear
                var query = from a in _db.Claim_Audits.AsNoTracking()
                           join c in _db.Claims.AsNoTracking() on a.ClaFID equals c.ClaID
                           join p in _db.Patients.AsNoTracking() on c.ClaPatFID equals p.PatID into patientGroup
                           from p in patientGroup.DefaultIfEmpty()
                           join rend in _db.Physicians.AsNoTracking() on c.ClaRenderingPhyFID equals rend.PhyID into rendGrp
                           from rend in rendGrp.DefaultIfEmpty()
                           join bill in _db.Physicians.AsNoTracking() on c.ClaBillingPhyFID equals bill.PhyID into billGrp
                           from bill in billGrp.DefaultIfEmpty()
                           join fac in _db.Physicians.AsNoTracking() on c.ClaFacilityPhyFID equals fac.PhyID into facGrp
                           from fac in facGrp.DefaultIfEmpty()
                           select new
                           {
                               a.AuditID,
                               ClaID = a.ClaFID,
                               a.ActivityDate,
                               a.UserName,
                               a.Notes,
                               a.ActivityType,
                               a.TotalCharge,
                               a.InsuranceBalance,
                               a.PatientBalance,
                               // Claim fields (all claim list columns)
                               c.ClaStatus,
                               c.ClaDateTimeCreated,
                               c.ClaDateTimeModified,
                               c.ClaTotalChargeTRIG,
                               c.ClaTotalBalanceCC,
                               c.ClaClassification,
                               c.ClaFirstDateTRIG,
                               c.ClaLastDateTRIG,
                               c.ClaBillDate,
                               c.ClaBillTo,
                               c.ClaPatFID,
                               c.ClaTypeOfBill,
                               c.ClaAdmissionType,
                               c.ClaPatientStatus,
                               c.ClaDiagnosis1,
                               c.ClaDiagnosis2,
                               c.ClaDiagnosis3,
                               c.ClaDiagnosis4,
                               // Match GetClaims: names on Claim row are often empty; audit trail has the user.
                               ClaCreatedUserName = c.ClaCreatedUserName
                                   ?? _db.Claim_Audits
                                       .Where(a2 => a2.ClaFID == c.ClaID)
                                       .OrderBy(a2 => a2.ActivityDate)
                                       .Select(a2 => a2.UserName)
                                       .FirstOrDefault(),
                               ClaLastUserName = c.ClaLastUserName
                                   ?? _db.Claim_Audits
                                       .Where(a2 => a2.ClaFID == c.ClaID)
                                       .OrderByDescending(a2 => a2.ActivityDate)
                                       .Select(a2 => a2.UserName)
                                       .FirstOrDefault(),
                               c.ClaRenderingPhyFID,
                               c.ClaBillingPhyFID,
                               c.ClaFacilityPhyFID,
                               // Patient
                               PatFullNameCC = p != null ? p.PatFullNameCC : null,
                               PatFirstName = p != null ? p.PatFirstName : null,
                               PatLastName = p != null ? p.PatLastName : null,
                               PatAccountNo = p != null ? p.PatAccountNo : null,
                               PatPhoneNo = p != null ? p.PatPhoneNo : null,
                               PatCity = p != null ? p.PatCity : null,
                               PatState = p != null ? p.PatState : null,
                               PatBirthDate = p != null ? p.PatBirthDate : (DateOnly?)null,
                               // Physicians (LEFT JOIN - avoids excluding rows when FK=0 or missing)
                               RenderingPhyName = rend != null ? rend.PhyName : null,
                               RenderingPhyNPI = rend != null ? rend.PhyNPI : null,
                               BillingPhyName = bill != null ? bill.PhyName : null,
                               BillingPhyNPI = bill != null ? bill.PhyNPI : null,
                               FacilityPhyName = fac != null ? fac.PhyName : null
                           };

                if (minClaimId.HasValue)
                    query = query.Where(x => x.ClaID >= minClaimId.Value);
                if (maxClaimId.HasValue)
                    query = query.Where(x => x.ClaID <= maxClaimId.Value);
                if (fromDate.HasValue)
                    query = query.Where(x => x.ActivityDate >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var endOfDay = toDate.Value.Date.AddDays(1);
                    query = query.Where(x => x.ActivityDate < endOfDay);
                }
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var q = searchText.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Notes != null && x.Notes.ToLower().Contains(q)) ||
                        (x.ActivityType != null && x.ActivityType.ToLower().Contains(q)) ||
                        (x.UserName != null && x.UserName.ToLower().Contains(q)));
                }

                var totalCount = await query.CountAsync();
                var data = await query
                    .OrderByDescending(x => x.ActivityDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = data.Select(x =>
                {
                    var noteText = !string.IsNullOrWhiteSpace(x.Notes) ? x.Notes : x.ActivityType;
                    var patientName = x.PatFullNameCC ?? (string.IsNullOrEmpty(x.PatFirstName) && string.IsNullOrEmpty(x.PatLastName) ? null : (x.PatFirstName + " " + x.PatLastName).Trim());
                    var addCols = new Dictionary<string, object?>();
                    if (hasPatFirstName) addCols["patFirstName"] = x.PatFirstName;
                    if (hasPatLastName) addCols["patLastName"] = x.PatLastName;
                    if (hasPatFullNameCC) addCols["patFullNameCC"] = x.PatFullNameCC;
                    if (hasPatAccountNo) addCols["patAccountNo"] = x.PatAccountNo;
                    if (hasPatPhoneNo) addCols["patPhoneNo"] = x.PatPhoneNo;
                    if (hasPatCity) addCols["patCity"] = x.PatCity;
                    if (hasPatState) addCols["patState"] = x.PatState;
                    if (hasPatBirthDate) addCols["patBirthDate"] = x.PatBirthDate;
                    if (hasRenderingPhyName) addCols["renderingPhyName"] = x.RenderingPhyName;
                    if (hasRenderingPhyNPI) addCols["renderingPhyNPI"] = x.RenderingPhyNPI;
                    if (hasBillingPhyName) addCols["billingPhyName"] = x.BillingPhyName;
                    if (hasBillingPhyNPI) addCols["billingPhyNPI"] = x.BillingPhyNPI;
                    if (hasFacilityName) addCols["facilityName"] = x.FacilityPhyName;
                    if (hasCreatedUser)
                    {
                        addCols["claCreatedUser"] = x.ClaCreatedUserName;
                        addCols["claCreatedUserName"] = x.ClaCreatedUserName;
                    }
                    if (hasModifiedUser)
                    {
                        addCols["claModifiedUser"] = x.ClaLastUserName;
                        addCols["claLastUserName"] = x.ClaLastUserName;
                    }

                    return new
                    {
                        x.AuditID,
                        x.ClaID,
                        activityDate = x.ActivityDate,
                        userName = x.UserName ?? "SYSTEM",
                        noteText,
                        x.TotalCharge,
                        x.InsuranceBalance,
                        x.PatientBalance,
                        patientName,
                        // Claim list columns
                        claStatus = x.ClaStatus,
                        claDateTimeCreated = x.ClaDateTimeCreated,
                        claDateTimeModified = x.ClaDateTimeModified,
                        createdDate = x.ActivityDate,
                        modifiedDate = x.ActivityDate,
                        claTotalChargeTRIG = x.ClaTotalChargeTRIG,
                        claTotalBalanceCC = x.ClaTotalBalanceCC,
                        claClassification = x.ClaClassification,
                        claFirstDateTRIG = x.ClaFirstDateTRIG,
                        claLastDateTRIG = x.ClaLastDateTRIG,
                        claBillDate = x.ClaBillDate,
                        claBillTo = x.ClaBillTo,
                        claPatFID = x.ClaPatFID,
                        claTypeOfBill = x.ClaTypeOfBill,
                        claAdmissionType = x.ClaAdmissionType,
                        claPatientStatus = x.ClaPatientStatus,
                        claDiagnosis1 = x.ClaDiagnosis1,
                        claDiagnosis2 = x.ClaDiagnosis2,
                        claDiagnosis3 = x.ClaDiagnosis3,
                        claDiagnosis4 = x.ClaDiagnosis4,
                        claCreatedUserName = x.ClaCreatedUserName,
                        claLastUserName = x.ClaLastUserName,
                        patFullNameCC = x.PatFullNameCC,
                        patFirstName = x.PatFirstName,
                        patLastName = x.PatLastName,
                        patAccountNo = x.PatAccountNo,
                        additionalColumns = addCols
                    };
                }).ToList();

                return Ok(new
                {
                    data = items,
                    meta = new { page, pageSize, totalCount }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Claim_Audit/GetClaimNotes failed. Table may not exist.");
                return Ok(new { data = Array.Empty<object>(), meta = new { page, pageSize, totalCount = 0 } });
            }
        }

        [HttpGet("{claId:int}")]
        public async Task<IActionResult> GetClaimById([FromRoute] int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Claim ID must be greater than 0"
                });
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var tid = _currentContext.TenantId;
                var fid = _currentContext.FacilityId;
                var claimHeader = await _db.Claims
                    .AsNoTracking()
                    .Where(c => c.ClaID == claId && c.TenantId == tid && c.FacilityId == fid)
                    .Select(c => new
                    {
                        c.ClaID,
                        c.ClaPatFID,
                        c.ClaStatus,
                        c.ClaCreatedUserName,
                        c.ClaLastUserName,
                        ClaDateTimeCreated = c.ClaDateTimeCreated,
                        ClaDateTimeModified = c.ClaDateTimeModified,
                        c.ClaTotalChargeTRIG,
                        c.ClaTotalAmtPaidCC,
                        c.ClaTotalBalanceCC,
                        c.ClaTotalAmtAppliedCC,
                        ClaBillDate = c.ClaBillDate.HasValue ? c.ClaBillDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaBillTo,
                        c.ClaSubmissionMethod,
                        c.ClaInvoiceNumber,
                        c.ClaLocked,
                        c.ClaOriginalRefNo,
                        c.ClaDelayCode,
                        c.ClaMedicaidResubmissionCode,
                        c.ClaPaperWorkTransmissionCode,
                        c.ClaPaperWorkControlNumber,
                        c.ClaPaperWorkInd,
                        c.ClaEDINotes,
                        c.ClaRemarks,
                        ClaAdmittedDate = c.ClaAdmittedDate.HasValue ? c.ClaAdmittedDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaDischargedDate = c.ClaDischargedDate.HasValue ? c.ClaDischargedDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaDateLastSeen = c.ClaDateLastSeen.HasValue ? c.ClaDateLastSeen.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaRelatedTo,
                        c.ClaRelatedToState,
                        ClaFirstDateTRIG = c.ClaFirstDateTRIG.HasValue ? c.ClaFirstDateTRIG.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaLastDateTRIG = c.ClaLastDateTRIG.HasValue ? c.ClaLastDateTRIG.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaClassification,
                        c.ClaDiagnosis1,
                        c.ClaDiagnosis2,
                        c.ClaDiagnosis3,
                        c.ClaDiagnosis4,
                        c.ClaDiagnosis5,
                        c.ClaDiagnosis6,
                        c.ClaDiagnosis7,
                        c.ClaDiagnosis8,
                        c.ClaDiagnosis9,
                        c.ClaDiagnosis10,
                        c.ClaDiagnosis11,
                        c.ClaDiagnosis12,
                        Patient = c.ClaPatF == null ? null : new
                        {
                            c.ClaPatF.PatID,
                            c.ClaPatF.PatFirstName,
                            c.ClaPatF.PatLastName,
                            c.ClaPatF.PatFullNameCC,
                            PatBirthDate = c.ClaPatF.PatBirthDate.HasValue ? c.ClaPatF.PatBirthDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                            c.ClaPatF.PatAccountNo,
                            c.ClaPatF.PatPhoneNo,
                            c.ClaPatF.PatCity,
                            c.ClaPatF.PatState
                        },
                        RenderingPhysician = c.ClaRenderingPhyF == null ? null : new { c.ClaRenderingPhyF.PhyID, c.ClaRenderingPhyF.PhyName, c.ClaRenderingPhyF.PhyNPI },
                        ReferringPhysician = c.ClaReferringPhyF == null ? null : new { c.ClaReferringPhyF.PhyID, c.ClaReferringPhyF.PhyName, c.ClaReferringPhyF.PhyNPI },
                        BillingPhysician = c.ClaBillingPhyF == null ? null : new { c.ClaBillingPhyF.PhyID, c.ClaBillingPhyF.PhyName, c.ClaBillingPhyF.PhyNPI },
                        FacilityPhysician = c.ClaFacilityPhyF == null ? null : new { c.ClaFacilityPhyF.PhyID, c.ClaFacilityPhyF.PhyName, c.ClaFacilityPhyF.PhyNPI },
                    })
                    .FirstOrDefaultAsync(cts.Token);

                if (claimHeader == null) return NotFound();

                var claimInsured = await _db.Claim_Insureds
                    .AsNoTracking()
                    .Where(ci => ci.ClaInsClaFID == claId)
                    .Select(ci => new
                    {
                        ci.ClaInsGUID,
                        ci.ClaInsSequence,
                        ci.ClaInsPayFID,
                        PayerName = ci.ClaInsPayF != null ? ci.ClaInsPayF.PayName : null
                    })
                    .ToListAsync(cts.Token);

                var primaryPayerName = claimInsured
                    .Where(i => i.ClaInsSequence == 1)
                    .Select(i => i.PayerName)
                    .FirstOrDefault();
                var secondaryPayerName = claimInsured
                    .Where(i => i.ClaInsSequence == 2)
                    .Select(i => i.PayerName)
                    .FirstOrDefault();
                var billToDisplay = claimHeader.ClaBillTo switch
                {
                    1 => $"P - {primaryPayerName ?? "PRIMARY"}",
                    2 => $"S - {secondaryPayerName ?? "SECONDARY"}",
                    _ => "Patient"
                };

                var serviceLineRows = await _db.Service_Lines
                    .AsNoTracking()
                    .Where(s => s.SrvClaFID == claId)
                    .OrderBy(s => s.SrvID)
                    .Select(s => new
                    {
                        s.SrvID,
                        SrvFromDate = s.SrvFromDate != default(DateOnly) ? s.SrvFromDate.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        SrvToDate = s.SrvToDate != default(DateOnly) ? s.SrvToDate.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvPlace,
                        s.SrvDiagnosisPointer,
                        s.SrvTotalInsAmtPaidTRIG,
                        s.SrvTotalPatAmtPaidTRIG,
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        s.SrvTotalAdjCC,
                        s.SrvTotalAmtAppliedCC,
                        s.SrvResponsibleParty,
                        ResponsiblePartyName = s.SrvResponsibleParty == 0
                            ? "Patient"
                            : (s.SrvResponsiblePartyNavigation != null ? s.SrvResponsiblePartyNavigation.PayName : null)
                    })
                    .ToListAsync(cts.Token);

                var serviceLineIds = serviceLineRows.Select(s => s.SrvID).ToList();

                var adjustmentRows = serviceLineIds.Count == 0
                    ? new List<(int AdjSrvFID, int AdjID, DateTime? AdjDate, decimal AdjAmount, string AdjGroupCode, string? AdjReasonCode, DateTime AdjDateTimeCreated, string? PayerName)>()
                    : (await _db.Adjustments
                        .AsNoTracking()
                        .Where(a => serviceLineIds.Contains(a.AdjSrvFID))
                        .Select(a => new
                        {
                            a.AdjID,
                            a.AdjSrvFID,
                            AdjDate = a.AdjDate.HasValue ? a.AdjDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                            a.AdjAmount,
                            a.AdjGroupCode,
                            a.AdjReasonCode,
                            AdjDateTimeCreated = a.AdjDateTimeCreated,
                            PayerName = a.AdjPayF != null ? a.AdjPayF.PayName : null
                        })
                        .ToListAsync(cts.Token))
                        .Select(a => (
                            AdjSrvFID: a.AdjSrvFID,
                            AdjID: a.AdjID,
                            AdjDate: a.AdjDate,
                            AdjAmount: a.AdjAmount,
                            AdjGroupCode: a.AdjGroupCode ?? string.Empty,
                            AdjReasonCode: a.AdjReasonCode,
                            AdjDateTimeCreated: a.AdjDateTimeCreated,
                            PayerName: a.PayerName))
                        .ToList();

                var disbursementRows = serviceLineIds.Count == 0
                    ? new List<(int DisbSrvFID, int DisbID, decimal DisbAmount, DateTime DisbDateTimeCreated, object? Payment)>()
                    : (await _db.Disbursements
                        .AsNoTracking()
                        .Where(d => serviceLineIds.Contains(d.DisbSrvFID))
                        .Select(d => new
                        {
                            d.DisbID,
                            d.DisbSrvFID,
                            d.DisbAmount,
                            d.DisbDateTimeCreated,
                            Payment = d.DisbPmtF == null ? null : new
                            {
                                d.DisbPmtF.PmtID,
                                PmtDate = d.DisbPmtF.PmtDate.ToDateTime(TimeOnly.MinValue),
                                d.DisbPmtF.PmtAmount,
                                d.DisbPmtF.PmtMethod,
                                d.DisbPmtF.Pmt835Ref,
                                d.DisbPmtF.PmtDateTimeCreated
                            }
                        })
                        .ToListAsync(cts.Token))
                        .Select(d => (
                            DisbSrvFID: d.DisbSrvFID,
                            DisbID: d.DisbID,
                            DisbAmount: d.DisbAmount,
                            DisbDateTimeCreated: d.DisbDateTimeCreated,
                            Payment: (object?)d.Payment))
                        .ToList();

                var claimActivity = await _db.Claim_Audits
                    .AsNoTracking()
                    .Where(a => a.ClaFID == claId)
                    .OrderByDescending(a => a.ActivityDate)
                    .Take(50)
                    .Select(a => new
                    {
                        date = a.ActivityDate,
                        user = a.UserName ?? "SYSTEM",
                        activityType = a.ActivityType,
                        notes = a.Notes,
                        totalCharge = a.TotalCharge,
                        insuranceBalance = a.InsuranceBalance,
                        patientBalance = a.PatientBalance
                    })
                    .ToListAsync(cts.Token);

                var adjustmentsByServiceLine = adjustmentRows
                    .GroupBy(a => a.AdjSrvFID)
                    .ToDictionary(g => g.Key, g => g.Select(a => (object)new
                    {
                        a.AdjID,
                        a.AdjDate,
                        a.AdjAmount,
                        a.AdjGroupCode,
                        a.AdjReasonCode,
                        a.AdjDateTimeCreated,
                        a.PayerName
                    }).ToList());

                var disbursementsByServiceLine = disbursementRows
                    .GroupBy(d => d.DisbSrvFID)
                    .ToDictionary(g => g.Key, g => g.Select(d => (object)new
                    {
                        d.DisbID,
                        d.DisbAmount,
                        d.DisbDateTimeCreated,
                        d.Payment
                    }).ToList());

                var serviceLines = serviceLineRows.Select(s => new
                {
                    s.SrvID,
                    s.SrvFromDate,
                    s.SrvToDate,
                    s.SrvProcedureCode,
                    s.SrvDesc,
                    s.SrvCharges,
                    s.SrvUnits,
                    s.SrvPlace,
                    s.SrvDiagnosisPointer,
                    s.SrvTotalInsAmtPaidTRIG,
                    s.SrvTotalPatAmtPaidTRIG,
                    s.SrvTotalBalanceCC,
                    s.SrvTotalAmtPaidCC,
                    s.SrvTotalAdjCC,
                    s.SrvTotalAmtAppliedCC,
                    s.SrvResponsibleParty,
                    s.ResponsiblePartyName,
                    Adjustments = adjustmentsByServiceLine.TryGetValue(s.SrvID, out var adj) ? adj : new List<object>(),
                    Disbursements = disbursementsByServiceLine.TryGetValue(s.SrvID, out var disb) ? disb : new List<object>()
                }).ToList();

                var claim = new
                {
                    claimHeader.ClaID,
                    claimHeader.ClaPatFID,
                    claimHeader.ClaStatus,
                    claimHeader.ClaCreatedUserName,
                    claimHeader.ClaLastUserName,
                    claimHeader.ClaDateTimeCreated,
                    claimHeader.ClaDateTimeModified,
                    claimHeader.ClaTotalChargeTRIG,
                    claimHeader.ClaTotalAmtPaidCC,
                    claimHeader.ClaTotalBalanceCC,
                    claimHeader.ClaTotalAmtAppliedCC,
                    claimHeader.ClaBillDate,
                    claimHeader.ClaBillTo,
                    billToDisplay,
                    primaryPayerName,
                    secondaryPayerName,
                    claimHeader.ClaSubmissionMethod,
                    claimHeader.ClaInvoiceNumber,
                    claimHeader.ClaLocked,
                    claimHeader.ClaOriginalRefNo,
                    claimHeader.ClaDelayCode,
                    claimHeader.ClaMedicaidResubmissionCode,
                    claimHeader.ClaPaperWorkTransmissionCode,
                    claimHeader.ClaPaperWorkControlNumber,
                    claimHeader.ClaPaperWorkInd,
                    claimHeader.ClaEDINotes,
                    claimHeader.ClaRemarks,
                    claimHeader.ClaAdmittedDate,
                    claimHeader.ClaDischargedDate,
                    claimHeader.ClaDateLastSeen,
                    claimHeader.ClaRelatedTo,
                    claimHeader.ClaRelatedToState,
                    claimHeader.ClaFirstDateTRIG,
                    claimHeader.ClaLastDateTRIG,
                    claimHeader.ClaClassification,
                    claimHeader.ClaDiagnosis1,
                    claimHeader.ClaDiagnosis2,
                    claimHeader.ClaDiagnosis3,
                    claimHeader.ClaDiagnosis4,
                    claimHeader.ClaDiagnosis5,
                    claimHeader.ClaDiagnosis6,
                    claimHeader.ClaDiagnosis7,
                    claimHeader.ClaDiagnosis8,
                    claimHeader.ClaDiagnosis9,
                    claimHeader.ClaDiagnosis10,
                    claimHeader.ClaDiagnosis11,
                    claimHeader.ClaDiagnosis12,
                    claimHeader.Patient,
                    claimHeader.RenderingPhysician,
                    claimHeader.ReferringPhysician,
                    claimHeader.BillingPhysician,
                    claimHeader.FacilityPhysician,
                    ClaimInsured = claimInsured,
                    ServiceLines = serviceLines,
                    ClaimActivity = claimActivity
                };

                return Ok(claim);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetClaimById query timed out for claim ID: {ClaId}", claId);
                return StatusCode(503, new ErrorResponseDto
                {
                    ErrorCode = "QUERY_TIMEOUT",
                    Message = "The query took too long to execute. Please try again or contact support if the issue persists."
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
            {
                _logger.LogWarning(sqlEx, "GetClaimById SQL timeout for claim ID: {ClaId}", claId);
                return StatusCode(503, new ErrorResponseDto
                {
                    ErrorCode = "QUERY_TIMEOUT",
                    Message = "The query took too long to execute. Please try again or contact support if the issue persists."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting claim by ID: {ClaId}", claId);
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "INTERNAL_ERROR",
                    Message = "An error occurred while retrieving the claim details."
                });
            }
        }

        /// <summary>
        /// Update claim fields. ClaClassification (Facility) values come from Libraries → List → Claim Classification.
        /// </summary>
        [HttpPut("{claId:int}")]
        public async Task<IActionResult> UpdateClaim([FromRoute] int claId, [FromBody] Zebl.Application.Dtos.Claims.UpdateClaimRequest request)
        {
            if (claId <= 0)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Invalid claim ID" });
            if (request == null)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Request body is required" });

            if (!string.IsNullOrWhiteSpace(request.ClaStatus) && !ClaimStatusCatalog.IsValidStoredValue(request.ClaStatus))
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Invalid claim status." });
            }

            try
            {
                var claim = await _db.Claims
                    .FirstOrDefaultAsync(c => c.ClaID == claId);
                if (claim == null)
                    return NotFound();
                var previousBillTo = claim.ClaBillTo;

                if (request.ClaBillTo.HasValue && !ClaimBillToRules.IsValidValue(request.ClaBillTo.Value))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = "ClaBillTo must be one of 0 (Patient), 1 (Primary), 2 (Secondary/Final)."
                    });
                }

                if (!request.PrimaryPayerId.HasValue || request.PrimaryPayerId.Value <= 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = "PrimaryPayerId is required and must be greater than 0."
                    });
                }

                var primaryClaimInsured = await _db.Claim_Insureds
                    .FirstOrDefaultAsync(i => i.ClaInsClaFID == claId && i.ClaInsSequence == 1);

                if (primaryClaimInsured == null)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = "Primary claim insurance row is missing for this claim."
                    });
                }

                var payloadPayerId = request.PrimaryPayerId.Value;
                var previousPrimaryPayerId = primaryClaimInsured.ClaInsPayFID;
                primaryClaimInsured.ClaInsPayFID = payloadPayerId;
                _logger.LogInformation("Claim {id} payer updated to {payerId} in ClaimInsurance", claId, payloadPayerId);

                _logger.LogInformation(
                    "Claim {id} save payer trace: payloadPayerId={payloadPayerId}, storedClaInsPayFID={storedClaInsPayFID}",
                    claId,
                    payloadPayerId,
                    primaryClaimInsured.ClaInsPayFID);

                var effectivePrimaryPayerId = primaryClaimInsured.ClaInsPayFID;

                // Insurance presence check:
                // - "SELF PAY" (HL7 fallback payer) is treated as "no insurance" for Bill To purposes.
                var hasInsurance = effectivePrimaryPayerId > 0 && await _db.Payers
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.PayID == effectivePrimaryPayerId &&
                        p.TenantId == claim.TenantId &&
                        p.FacilityId == claim.FacilityId &&
                        p.PayName != "SELF PAY");

                // Resolve deterministically; never persist null.
                claim.ClaBillTo = ClaimBillToRules.Resolve(request.ClaBillTo, claim.ClaBillTo, hasInsurance);
                var didBillToChange = previousBillTo != claim.ClaBillTo;
                var didPrimaryPayerChange = previousPrimaryPayerId != payloadPayerId;
                _logger.LogInformation(
                    "Claim update payer trace. claimId={claimId}, previousClaBillTo={previousClaBillTo}, newClaBillTo={newClaBillTo}, previousPrimaryPayerId={previousPrimaryPayerId}, newPrimaryPayerId={newPrimaryPayerId}, didBillToChange={didBillToChange}, didPrimaryPayerChange={didPrimaryPayerChange}",
                    claId,
                    previousBillTo,
                    claim.ClaBillTo,
                    previousPrimaryPayerId,
                    payloadPayerId,
                    didBillToChange,
                    didPrimaryPayerChange);

                claim.ClaClassification = request.ClaClassification != null
                    ? (request.ClaClassification.Length > 30 ? request.ClaClassification[..30] : request.ClaClassification)
                    : null;
                if (!string.IsNullOrWhiteSpace(request.ClaStatus))
                {
                    claim.ClaStatus = request.ClaStatus;
                }
                claim.ClaSubmissionMethod = request.ClaSubmissionMethod;
                if (request.ClaRenderingPhyFID.HasValue)
                    claim.ClaRenderingPhyFID = request.ClaRenderingPhyFID.Value;
                if (request.ClaFacilityPhyFID.HasValue)
                    claim.ClaFacilityPhyFID = request.ClaFacilityPhyFID.Value;
                claim.ClaInvoiceNumber = request.ClaInvoiceNumber;
                if (request.ClaAdmittedDate.HasValue)
                    claim.ClaAdmittedDate = DateOnly.FromDateTime(request.ClaAdmittedDate.Value);
                if (request.ClaDischargedDate.HasValue)
                    claim.ClaDischargedDate = DateOnly.FromDateTime(request.ClaDischargedDate.Value);
                if (request.ClaDateLastSeen.HasValue)
                    claim.ClaDateLastSeen = DateOnly.FromDateTime(request.ClaDateLastSeen.Value);
                if (request.ClaBillDate.HasValue)
                    claim.ClaBillDate = DateOnly.FromDateTime(request.ClaBillDate.Value);
                if (request.ClaEDINotes != null)
                    claim.ClaEDINotes = request.ClaEDINotes;
                if (request.ClaRemarks != null)
                    claim.ClaRemarks = request.ClaRemarks;
                if (request.ClaRelatedTo.HasValue)
                {
                    // Ensure the int value fits into the target short? property to avoid data loss
                    if (request.ClaRelatedTo.Value < short.MinValue || request.ClaRelatedTo.Value > short.MaxValue)
                    {
                        return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "ClaRelatedTo value is out of range" });
                    }

                    claim.ClaRelatedTo = (short?)request.ClaRelatedTo.Value;
                }
                if (request.ClaRelatedToState != null)
                    claim.ClaRelatedToState = request.ClaRelatedToState;
                if (request.ClaLocked.HasValue)
                    claim.ClaLocked = request.ClaLocked.Value;
                claim.ClaDelayCode = request.ClaDelayCode != null && request.ClaDelayCode.Length > 2 ? request.ClaDelayCode[..2] : request.ClaDelayCode;
                claim.ClaMedicaidResubmissionCode = request.ClaMedicaidResubmissionCode != null && request.ClaMedicaidResubmissionCode.Length > 50 ? request.ClaMedicaidResubmissionCode[..50] : request.ClaMedicaidResubmissionCode;
                claim.ClaOriginalRefNo = request.ClaOriginalRefNo != null && request.ClaOriginalRefNo.Length > 80 ? request.ClaOriginalRefNo[..80] : request.ClaOriginalRefNo;
                claim.ClaPaperWorkTransmissionCode = request.ClaPaperWorkTransmissionCode != null && request.ClaPaperWorkTransmissionCode.Length > 2 ? request.ClaPaperWorkTransmissionCode[..2] : request.ClaPaperWorkTransmissionCode;
                claim.ClaPaperWorkControlNumber = request.ClaPaperWorkControlNumber != null && request.ClaPaperWorkControlNumber.Length > 80 ? request.ClaPaperWorkControlNumber[..80] : request.ClaPaperWorkControlNumber;
                claim.ClaPaperWorkInd = request.ClaPaperWorkInd != null && request.ClaPaperWorkInd.Length > 20 ? request.ClaPaperWorkInd[..20] : request.ClaPaperWorkInd;

                if (request.AdditionalData != null)
                {
                    claim.ClaAdditionalData = SerializeClaimAdditionalData(request.AdditionalData);
                }

                var serviceLineSnapshot = await _db.Service_Lines
                    .AsNoTracking()
                    .Where(s => s.SrvClaFID == claId)
                    .OrderBy(s => s.SrvID)
                    .Select(s => new { s.SrvID, s.SrvProcedureCode })
                    .ToListAsync();
                var serviceLineIds = serviceLineSnapshot.Select(s => s.SrvID.ToString()).ToList();
                var serviceLineProcCodes = serviceLineSnapshot.Select(s => $"{s.SrvID}:{s.SrvProcedureCode}").ToList();

                _logger.LogInformation(
                    "Updating claim {ClaId}. Request snapshot: Status={Status}, SubmissionMethod={SubmissionMethod}, RenderingPhy={RenderingPhy}, FacilityPhy={FacilityPhy}, BillingPhy={BillingPhy}, Locked={Locked}, ServiceLineIds={ServiceLineIds}, ServiceLineProcs={ServiceLineProcs}",
                    claId,
                    request.ClaStatus,
                    request.ClaSubmissionMethod,
                    claim.ClaRenderingPhyFID,
                    claim.ClaFacilityPhyFID,
                    claim.ClaBillingPhyFID,
                    request.ClaLocked,
                    string.Join(",", serviceLineIds),
                    string.Join(",", serviceLineProcCodes));

                // Prepare claim audit before save so the request performs a single SaveChangesAsync().
                var userName = _userContext.UserName ?? "SYSTEM";
                var computerName = _userContext.ComputerName ?? Environment.MachineName;
                var noteText = !string.IsNullOrWhiteSpace(request.NoteText)
                    ? request.NoteText.Trim().Length > 500 ? request.NoteText.Trim()[..500] : request.NoteText.Trim()
                    : "Claim edited.";
                _db.Claim_Audits.Add(new Claim_Audit
                {
                    TenantId = claim.TenantId,
                    FacilityId = claim.FacilityId,
                    ClaFID = claId,
                    ActivityType = "Claim Edited",
                    ActivityDate = DateTime.UtcNow,
                    UserName = userName,
                    ComputerName = computerName,
                    Notes = noteText,
                    TotalCharge = claim.ClaTotalChargeTRIG,
                    InsuranceBalance = claim.ClaTotalInsBalanceTRIG,
                    PatientBalance = claim.ClaTotalPatBalanceTRIG
                });

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    var inner = dbEx.InnerException?.Message;
                    var trackedEntries = string.Join(", ", dbEx.Entries.Select(e => e.Entity.GetType().Name));
                    _logger.LogError(
                        dbEx,
                        "DbUpdateException while updating claim {ClaId}. Tracked entities: {TrackedEntries}. Inner: {InnerMessage}",
                        claId,
                        trackedEntries,
                        inner);
                    _logger.LogError("SQL error: {Message}", dbEx.InnerException?.Message);
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "FK_VALIDATION",
                        Message = "Claim update failed due to invalid related data (e.g., physician reference)."
                    });
                }

                if (didBillToChange || didPrimaryPayerChange)
                {
                    _logger.LogInformation(
                        "CALLING SYNC for claimId={claimId}, payerId={payerId}",
                        claId,
                        payloadPayerId);

                    var updatedRows = await _serviceLineRepository.UpdateServiceLineResponsibleParty(claId, payloadPayerId);
                    _logger.LogInformation(
                        "After service-line sync: rowsAffected={rowsAffected}",
                        updatedRows);

                    var serviceLineRows = await _db.Service_Lines
                        .AsNoTracking()
                        .Where(s => s.SrvClaFID == claId)
                        .OrderBy(s => s.SrvID)
                        .Select(s => new { s.SrvID, s.SrvResponsibleParty })
                        .ToListAsync();
                    var snapshot = string.Join(", ", serviceLineRows.Select(r => $"{r.SrvID}:{r.SrvResponsibleParty}"));
                    _logger.LogInformation(
                        "Post-sync Service_Line snapshot. claimId={claimId}, rows={rows}",
                        claId,
                        snapshot);
                }

                var persistedPrimaryPayerId = await _db.Claim_Insureds
                    .AsNoTracking()
                    .Where(i => i.ClaInsClaFID == claId && i.ClaInsSequence == 1)
                    .Select(i => i.ClaInsPayFID)
                    .FirstOrDefaultAsync();

                _logger.LogInformation(
                    "Claim {id} save payer trace: payloadPayerId={payloadPayerId}, storedClaInsPayFID={storedClaInsPayFID}",
                    claId,
                    payloadPayerId,
                    persistedPrimaryPayerId);

                if (persistedPrimaryPayerId != payloadPayerId)
                {
                    throw new InvalidOperationException(
                        $"Primary payer mismatch after save for claim {claId}. payloadPayerId={payloadPayerId}, storedClaInsPayFID={persistedPrimaryPayerId}");
                }

                _logger.LogInformation("Updated claim {ClaId}, ClaClassification={ClaClassification}", claId, claim.ClaClassification);
                _logger.LogInformation("Claim {id} saved with payer {payerId}", claId, effectivePrimaryPayerId);
                return Ok(new
                {
                    claim.ClaID,
                    claim.ClaTotalChargeTRIG,
                    claim.ClaTotalAmtPaidCC,
                    claim.ClaTotalBalanceCC,
                    claim.ClaTotalAmtAppliedCC
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating claim {ClaId}. Payload summary: Status={Status}, SubmissionMethod={SubmissionMethod}, RenderingPhy={RenderingPhy}, FacilityPhy={FacilityPhy}, RelatedTo={RelatedTo}",
                    claId,
                    request?.ClaStatus,
                    request?.ClaSubmissionMethod,
                    request?.ClaRenderingPhyFID,
                    request?.ClaFacilityPhyFID,
                    request?.ClaRelatedTo);
                return StatusCode(500, new ErrorResponseDto { ErrorCode = "INTERNAL_ERROR", Message = "Failed to update claim" });
            }
        }

        private static ClaimAdditionalData? DeserializeClaimAdditionalData(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(ClaimAdditionalData));
                using var reader = new StringReader(xml.Trim());
                return (ClaimAdditionalData?)serializer.Deserialize(reader);
            }
            catch
            {
                return null;
            }
        }

        private static string? SerializeClaimAdditionalData(ClaimAdditionalData data)
        {
            if (data == null) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(ClaimAdditionalData));
                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb))
                {
                    serializer.Serialize(writer, data);
                }
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task<int> GetApproxClaimCountAsync()
        {
            try
            {
                var tenantId = _currentContext.TenantId;
                var facilityId = _currentContext.FacilityId;
                return await _db.Claims
                    .AsNoTracking()
                    .Where(c => c.TenantId == tenantId && c.FacilityId == facilityId)
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }
    }




}


