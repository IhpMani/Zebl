using Microsoft.Extensions.Logging;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Repositories;
using Zebl.Application.Utilities;

namespace Zebl.Application.Services;

/// <summary>
/// Application service for EDI Reports: persistence, hashing, file store, and optional inbound post-processors.
/// </summary>
public class EdiReportService
{
    private readonly IEdiReportRepository _repository;
    private readonly IEdiProcessingLimiter _processingLimiter;
    private readonly IClaimPaymentIngestionService _claimPaymentIngestionService;
    private readonly IEdiReportFileStore _fileStore;
    private readonly ICurrentContext _currentContext;
    private readonly IEnumerable<IEdiInboundPostProcessor> _postProcessors;
    private readonly ILogger<EdiReportService> _logger;

    public EdiReportService(
        IEdiReportRepository repository,
        IEdiProcessingLimiter processingLimiter,
        IClaimPaymentIngestionService claimPaymentIngestionService,
        IEdiReportFileStore fileStore,
        ICurrentContext currentContext,
        IEnumerable<IEdiInboundPostProcessor> postProcessors,
        ILogger<EdiReportService> logger)
    {
        _repository = repository;
        _processingLimiter = processingLimiter;
        _claimPaymentIngestionService = claimPaymentIngestionService;
        _fileStore = fileStore;
        _currentContext = currentContext;
        _postProcessors = postProcessors;
        _logger = logger;
    }

    public Task<List<EdiReport>> GetAllAsync(bool? isArchived = null) => _repository.GetAllAsync(isArchived);

    public Task<EdiReport?> GetByIdAsync(Guid id) => _repository.GetByIdAsync(id);

    public async Task<EdiReportCreateOutcome> CreateGeneratedAsync(
        Guid receiverLibraryId,
        Guid? connectionLibraryId,
        string fileName,
        string fileType,
        byte[] fileContent,
        string correlationId,
        string direction = "Outbound",
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        ArgumentNullException.ThrowIfNull(fileContent);
        _logger.LogInformation("EDI generated report persist start. CorrelationId={CorrelationId} ReceiverId={ReceiverId} FileName={FileName}", correlationId, receiverLibraryId, fileName);
        var tenantId = _currentContext.TenantId;
        var id = Guid.NewGuid();
        var hash = ContentHashUtility.Sha256Hex(fileContent);
        var storageKey = _fileStore.BuildStorageKey(tenantId, id, fileName);
        var report = new EdiReport
        {
            Id = id,
            TenantId = tenantId,
            ReceiverLibraryId = receiverLibraryId,
            ConnectionLibraryId = connectionLibraryId,
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
            FileType = fileType ?? throw new ArgumentNullException(nameof(fileType)),
            Direction = direction,
            Status = "PendingStorage",
            FileStorageKey = storageKey,
            ContentHashSha256 = hash,
            FileHash = hash,
            CorrelationId = correlationId,
            FileSize = fileContent.Length,
            IsRead = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(report).ConfigureAwait(false);
        try
        {
            await _fileStore.WriteAsync(storageKey, fileContent, cancellationToken).ConfigureAwait(false);
            report.Status = "Generated";
            await _repository.UpdateAsync(report).ConfigureAwait(false);
        }
        catch
        {
            await _repository.DeleteAsync(report.Id).ConfigureAwait(false);
            throw;
        }
        _logger.LogInformation("EDI generated report persist complete. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, report.Id);
        if (EdiOperationalMetrics.ShouldSampleProcessing())
        {
            EdiOperationalMetrics.ProcessingMs.Record(
                (DateTime.UtcNow - started).TotalMilliseconds,
                new KeyValuePair<string, object?>("flow", "outbound-generated"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        return new EdiReportCreateOutcome(report, IsDuplicate: false, ClpCount: null);
    }

    public async Task<EdiReportCreateOutcome> CreateReceivedAsync(
        Guid receiverLibraryId,
        Guid? connectionLibraryId,
        string fileName,
        string fileType,
        byte[] fileContent,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        await using var _slot = await _processingLimiter.AcquireInboundSlotAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(fileContent);
        if (fileContent.Length > 5 * 1024 * 1024)
            _logger.LogWarning("Large inbound EDI payload detected. CorrelationId={CorrelationId} SizeBytes={SizeBytes}", correlationId, fileContent.Length);
        _logger.LogInformation("EDI inbound persist start. CorrelationId={CorrelationId} ReceiverId={ReceiverId} FileName={FileName}", correlationId, receiverLibraryId, fileName);
        var tenantId = _currentContext.TenantId;
        var hash = ContentHashUtility.Sha256Hex(fileContent);
        _logger.LogInformation("EDI inbound file hash computed. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash}", correlationId, fileName, hash);
        var existing = await _repository.FindByFileHashAsync(tenantId, hash, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            _logger.LogInformation("EDI inbound skipped duplicate file by hash. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash} ExistingReportId={ReportId}", correlationId, fileName, hash, existing.Id);
            return new EdiReportCreateOutcome(existing, IsDuplicate: true, ClpCount: null);
        }
        var structured = await ParseInboundStructuredAsync(fileType, fileContent, correlationId, cancellationToken).ConfigureAwait(false);

        var id = Guid.NewGuid();
        var storageKey = _fileStore.BuildStorageKey(tenantId, id, fileName);
        var report = new EdiReport
        {
            Id = id,
            TenantId = tenantId,
            ReceiverLibraryId = receiverLibraryId,
            ConnectionLibraryId = connectionLibraryId,
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
            FileType = fileType ?? throw new ArgumentNullException(nameof(fileType)),
            Direction = "Inbound",
            Status = "PendingStorage",
            FileStorageKey = storageKey,
            ContentHashSha256 = hash,
            FileHash = hash,
            CorrelationId = correlationId,
            FileSize = fileContent.Length,
            TraceNumber = structured.TraceNumber,
            ClaimIdentifier = structured.ClaimIdentifier,
            PayerName = structured.PayerName,
            PaymentAmount = structured.PaymentAmount,
            Note = structured.Note,
            IsRead = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(report).ConfigureAwait(false);

        try
        {
            await _fileStore.WriteAsync(storageKey, fileContent, cancellationToken).ConfigureAwait(false);
            report.Status = "Received";
            await _repository.UpdateAsync(report).ConfigureAwait(false);
        }
        catch
        {
            await _repository.DeleteAsync(report.Id).ConfigureAwait(false);
            throw;
        }
        await RunInboundPostProcessorsAsync(report, fileContent, fileType, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("EDI inbound persist complete. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, report.Id);
        if (EdiOperationalMetrics.ShouldSampleProcessing())
        {
            EdiOperationalMetrics.ProcessingMs.Record(
                (DateTime.UtcNow - started).TotalMilliseconds,
                new KeyValuePair<string, object?>("flow", "inbound"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        return new EdiReportCreateOutcome(report, IsDuplicate: false, ClpCount: structured.ClpCount);
    }

    public async Task<EdiReportCreateOutcome> CreateReceivedFromMetadataAsync(
        Guid receiverLibraryId,
        Guid? connectionLibraryId,
        string fileName,
        string fileType,
        string correlationId,
        string? payerName = null,
        decimal? paymentAmount = null,
        string? note = null,
        string? traceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        await using var _slot = await _processingLimiter.AcquireInboundSlotAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("EDI inbound metadata persist start. CorrelationId={CorrelationId} ReceiverId={ReceiverId} FileName={FileName}", correlationId, receiverLibraryId, fileName);
        var tenantId = _currentContext.TenantId;
        var fingerprint = $"{fileName}|{fileType}|{payerName}|{paymentAmount}|{note}|{traceNumber}";
        var hash = ContentHashUtility.Sha256HexFromUtf8(fingerprint);

        var id = Guid.NewGuid();
        var storageKey = _fileStore.BuildStorageKey(tenantId, id, fileName);
        var report = new EdiReport
        {
            Id = id,
            TenantId = tenantId,
            ReceiverLibraryId = receiverLibraryId,
            ConnectionLibraryId = connectionLibraryId,
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
            FileType = fileType ?? throw new ArgumentNullException(nameof(fileType)),
            Direction = "Inbound",
            Status = "PendingStorage",
            FileStorageKey = storageKey,
            ContentHashSha256 = hash,
            FileHash = hash,
            CorrelationId = correlationId,
            FileSize = 0,
            PayerName = payerName,
            PaymentAmount = paymentAmount,
            Note = note != null && note.Length > 255 ? note[..255] : note,
            TraceNumber = traceNumber,
            IsRead = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(report).ConfigureAwait(false);
        try
        {
            await _fileStore.WriteAsync(storageKey, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            report.Status = "Received";
            await _repository.UpdateAsync(report).ConfigureAwait(false);
        }
        catch
        {
            await _repository.DeleteAsync(report.Id).ConfigureAwait(false);
            throw;
        }
        _logger.LogInformation("EDI inbound metadata persist complete. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, report.Id);
        if (EdiOperationalMetrics.ShouldSampleProcessing())
        {
            EdiOperationalMetrics.ProcessingMs.Record(
                (DateTime.UtcNow - started).TotalMilliseconds,
                new KeyValuePair<string, object?>("flow", "inbound-metadata"),
                new KeyValuePair<string, object?>("outcome", "success"));
        }
        return new EdiReportCreateOutcome(report, IsDuplicate: false, ClpCount: null);
    }

    private async Task RunInboundPostProcessorsAsync(EdiReport report, byte[] fileContent, string fileType, string correlationId, CancellationToken cancellationToken)
    {
        foreach (var processor in _postProcessors)
        {
            await processor.ProcessInboundPersistedAsync(report, fileContent, fileType, correlationId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<InboundStructuredSummary> ParseInboundStructuredAsync(string fileType, byte[] content, string correlationId, CancellationToken cancellationToken)
    {
        if (string.Equals(fileType, "835", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = new MemoryStream(content, writable: false);
            var r = await Edi835Parser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
            if (string.Equals(r.TraceNumber, "NoTrace", StringComparison.Ordinal))
                _logger.LogWarning("835 parse completed with missing TRN trace. CorrelationId={CorrelationId}", correlationId);
            else
                _logger.LogInformation("835 parsed TRN. CorrelationId={CorrelationId} TraceNumber={TraceNumber}", correlationId, r.TraceNumber);

            var ingestion = await _claimPaymentIngestionService.Ingest835Async(r, correlationId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "835 claim payment ingestion complete. CorrelationId={CorrelationId} Trace={Trace} ClpCount={ClpCount} Matched={Matched} Unmatched={Unmatched} Duplicates={Duplicates} Invalid={Invalid}",
                correlationId,
                r.TraceNumber,
                r.ClaimPayments.Count,
                ingestion.Matched,
                ingestion.Unmatched,
                ingestion.Duplicates,
                ingestion.Invalid);

            return new InboundStructuredSummary(
                r.PayerName,
                r.ClaimPaymentAmount,
                r.SummaryNote,
                r.TraceNumber,
                r.ClaimPayments.FirstOrDefault()?.ClaimId,
                r.ClaimPayments.Count);
        }

        if (string.Equals(fileType, "999", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = new MemoryStream(content, writable: false);
            var r = await Edi999Parser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
            return new InboundStructuredSummary(null, null, r.SummaryNote, null, null, null);
        }

        return new InboundStructuredSummary(null, null, null, null, null, null);
    }

    private sealed record InboundStructuredSummary(
        string? PayerName,
        decimal? PaymentAmount,
        string? Note,
        string? TraceNumber,
        string? ClaimIdentifier,
        int? ClpCount);

    public async Task MarkSentAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.Status = "Sent";
        report.SentAt = DateTime.UtcNow;
        await _repository.UpdateAsync(report);
    }

    public async Task MarkFailedAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.Status = "Failed";
        await _repository.UpdateAsync(report);
    }

    public async Task ArchiveAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.IsArchived = true;
        await _repository.UpdateAsync(report);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.IsRead = true;
        await _repository.UpdateAsync(report);
    }

    public async Task UpdateNoteAsync(Guid id, string? note)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        if (note != null && note.Length > 255)
            note = note[..255];
        report.Note = note;
        await _repository.UpdateAsync(report);
    }

    public async Task DeleteAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        if (!string.IsNullOrEmpty(report.FileStorageKey))
            await _fileStore.TryDeleteAsync(report.FileStorageKey).ConfigureAwait(false);
        await _repository.DeleteAsync(id);
    }
}

public sealed record EdiReportCreateOutcome(EdiReport Report, bool IsDuplicate, int? ClpCount = null);
