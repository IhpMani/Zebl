using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Application.Services.Edi;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/edi-reports")]
[Authorize(Policy = "RequireAuth")]
public class EdiReportsController : ControllerBase
{
    private readonly EdiReportService _ediReportService;
    private readonly IEdiGenerator _ediGenerator;
    private readonly IReceiverLibraryRepository _receiverLibraryRepository;
    private readonly IConnectionLibraryRepository _connectionRepo;
    private readonly SftpTransportService _sftpTransport;
    private readonly HttpInboundTransportService _httpInboundTransport;
    private readonly IEdiReportContentReader _contentReader;
    private readonly IEdiValidationService _ediValidationService;
    private readonly IEdiAutoPostService _ediAutoPostService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<EdiReportsController> _logger;

    public EdiReportsController(
        EdiReportService ediReportService,
        IEdiGenerator ediGenerator,
        IReceiverLibraryRepository receiverLibraryRepository,
        IConnectionLibraryRepository connectionRepo,
        SftpTransportService sftpTransport,
        HttpInboundTransportService httpInboundTransport,
        IEdiReportContentReader contentReader,
        IEdiValidationService ediValidationService,
        IEdiAutoPostService ediAutoPostService,
        ICurrentContext currentContext,
        ILogger<EdiReportsController> logger)
    {
        _ediReportService = ediReportService;
        _ediGenerator = ediGenerator;
        _receiverLibraryRepository = receiverLibraryRepository;
        _connectionRepo = connectionRepo;
        _sftpTransport = sftpTransport;
        _httpInboundTransport = httpInboundTransport;
        _contentReader = contentReader;
        _ediValidationService = ediValidationService;
        _ediAutoPostService = ediAutoPostService;
        _currentContext = currentContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? archived = null)
    {
        try
        {
            var list = await _ediReportService.GetAllAsync(archived);
            var dtoList = list.Select(r => new
            {
                r.Id,
                r.ReceiverLibraryId,
                r.ConnectionLibraryId,
                r.FileName,
                r.FileType,
                r.Direction,
                r.Status,
                r.TraceNumber,
                r.ClaimIdentifier,
                r.PayerName,
                r.PaymentAmount,
                r.Note,
                r.IsArchived,
                r.IsRead,
                r.FileSize,
                r.CorrelationId,
                r.CreatedAt,
                r.SentAt,
                r.ReceivedAt
            }).ToList();
            return Ok(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EDI reports");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();
        var dto = new
        {
            report.Id,
            report.ReceiverLibraryId,
            report.ConnectionLibraryId,
            report.FileName,
            report.FileType,
            report.Direction,
            report.Status,
            report.TraceNumber,
            report.ClaimIdentifier,
            report.PayerName,
            report.PaymentAmount,
            report.Note,
            report.IsArchived,
            report.IsRead,
            report.FileSize,
            report.CorrelationId,
            report.CreatedAt,
            report.SentAt,
            report.ReceivedAt
        };
        return Ok(dto);
    }

    /// <summary>
    /// Single endpoint: generates outbound EDI (837 or 270) from claim context and persists a report row + file.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateEdiReportRequest request)
    {
        if (request?.ReceiverLibraryId == null || request.ClaimId == null)
            return BadRequest(new { error = "ReceiverLibraryId and ClaimId are required." });

        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            var receiver = await _receiverLibraryRepository.GetByIdAsync(request.ReceiverLibraryId.Value).ConfigureAwait(false);
            if (receiver == null)
                return BadRequest(new { error = "Receiver library not found." });

            var kind = ResolveOutboundKind(request.FileType, receiver.ExportFormat);
            _logger.LogInformation(
                "EDI generate requested. CorrelationId={CorrelationId} ReceiverId={ReceiverId} ClaimId={ClaimId} Kind={Kind}",
                correlationId,
                request.ReceiverLibraryId,
                request.ClaimId,
                kind);

            var content = await _ediGenerator.GenerateAsync(
                request.ReceiverLibraryId.Value,
                request.ClaimId.Value,
                kind,
                HttpContext.RequestAborted).ConfigureAwait(false);
            _ediValidationService.Validate(content, kind);

            var fileType = kind == OutboundEdiKind.Eligibility270 ? "270" : "837";
            var fileName = $"report_{fileType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.edi";
            var fileContent = Encoding.UTF8.GetBytes(content);

            var report = await _ediReportService.CreateGeneratedAsync(
                request.ReceiverLibraryId.Value,
                request.ConnectionLibraryId,
                fileName,
                fileType,
                fileContent,
                correlationId,
                "Outbound",
                HttpContext.RequestAborted);

            return Ok(new
            {
                status = "Generated",
                correlationId,
                reportId = report.Report.Id,
                report.Report.FileName,
                report.Report.FileType,
                reportStatus = report.Report.Status,
                report.Report.FileSize,
                isDuplicate = report.IsDuplicate
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EDI generate rejected. CorrelationId={CorrelationId}", correlationId);
            return BadRequest(new { error = ex.Message, correlationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EDI report. CorrelationId={CorrelationId}", correlationId);
            return StatusCode(500, new { error = ex.Message, correlationId });
        }
    }

    [HttpPost("send/{id:guid}")]
    public async Task<IActionResult> Send(Guid id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        if (report.ConnectionLibraryId == null)
            return BadRequest(new { error = "Report has no connection library assigned." });

        var connection = await _connectionRepo.GetByIdAsync(report.ConnectionLibraryId.Value);
        if (connection == null)
            return BadRequest(new { error = "Connection library not found." });

        if (connection.ConnectionType != ConnectionType.Sftp)
            return BadRequest(new { error = "Send currently supports ConnectionType SFTP only." });

        try
        {
            await using var validationStream = await _contentReader.OpenReadAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
            using var validationReader = new StreamReader(validationStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 64, leaveOpen: true);
            var content = await validationReader.ReadToEndAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            var kind = string.Equals(report.FileType, "270", StringComparison.Ordinal) ? OutboundEdiKind.Eligibility270 : OutboundEdiKind.Claim837;
            _ediValidationService.Validate(content, kind);
            _logger.LogInformation(
                "EDI SFTP upload starting. CorrelationId={CorrelationId} ReportId={ReportId} FileName={FileName}",
                correlationId,
                id,
                report.FileName);
            await using var uploadStream = await _contentReader.OpenReadAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
            await _sftpTransport.UploadFileAsync(connection, report.FileName, uploadStream, HttpContext.RequestAborted);
            await _ediReportService.MarkSentAsync(id);
            return Ok(new { success = true, message = "File sent.", correlationId });
        }
        catch (EdiReportFileNotAvailableException ex)
        {
            _logger.LogWarning(ex, "EDI send failed: file missing. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
            return BadRequest(new { error = ex.Message, correlationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending EDI report {Id}. CorrelationId={CorrelationId}", id, correlationId);
            await _ediReportService.MarkFailedAsync(id);
            return StatusCode(500, new { error = ex.Message, correlationId });
        }
    }

    [HttpPost("download")]
    public async Task<IActionResult> Download([FromBody] DownloadEdiReportRequest request)
    {
        if (request?.ConnectionLibraryId == null || request.ReceiverLibraryId == null)
            return BadRequest(new { error = "ConnectionLibraryId and ReceiverLibraryId are required." });

        var correlationId = HttpContext.TraceIdentifier;
        var connection = await _connectionRepo.GetByIdAsync(request.ConnectionLibraryId.Value);
        if (connection == null)
            return NotFound(new { error = "Connection library not found." });

        var createdReports = new List<object>();
        var processedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        try
        {
            switch (connection.ConnectionType)
            {
                case ConnectionType.Http:
                case ConnectionType.Api:
                {
                    _logger.LogInformation(
                        "EDI inbound fetch via {Type}. CorrelationId={CorrelationId} ConnectionId={ConnectionId} ReceiverId={ReceiverId}",
                        connection.ConnectionType,
                        correlationId,
                        connection.Id,
                        request.ReceiverLibraryId);

                    var items = await _httpInboundTransport.FetchAsync(connection, HttpContext.RequestAborted).ConfigureAwait(false);
                    foreach (var item in items)
                    {
                        try
                        {
                            _logger.LogInformation("Processing file: {FileName}", item.FileName);
                            EdiReport report;
                            var hash = item.RawContent is { Length: > 0 }
                                ? Zebl.Application.Utilities.ContentHashUtility.Sha256Hex(item.RawContent)
                                : Zebl.Application.Utilities.ContentHashUtility.Sha256HexFromUtf8($"{item.FileName}|{item.FileType}|{item.PayerName}|{item.PaymentAmount}|{item.Note}|{item.TraceNumber}");
                            if (item.RawContent is { Length: > 0 })
                            {
                                var utf8 = Encoding.UTF8.GetString(item.RawContent);
                                var fileType = ResolveInboundFileType(item.FileName, utf8);
                                var create = await _ediReportService.CreateReceivedAsync(
                                    request.ReceiverLibraryId.Value,
                                    request.ConnectionLibraryId,
                                    item.FileName,
                                    fileType,
                                    item.RawContent,
                                    correlationId,
                                    HttpContext.RequestAborted);
                                report = create.Report;
                                if (create.IsDuplicate) skippedCount++;
                                else processedCount++;
                                _logger.LogInformation(
                                    "Inbound file lifecycle status. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash} Status={Status} ClpCount={ClpCount}",
                                    correlationId,
                                    item.FileName,
                                    hash,
                                    create.IsDuplicate ? "Skipped" : "Processed",
                                    create.ClpCount ?? 0);
                                createdReports.Add(new
                                {
                                    report.Id,
                                    report.FileName,
                                    report.FileType,
                                    report.Status,
                                    report.ClaimIdentifier,
                                    report.PayerName,
                                    report.PaymentAmount,
                                    report.Note,
                                    isDuplicate = create.IsDuplicate
                                });
                                continue;
                            }

                            var metadataType = NormalizeInboundTypeFromMetadata(item.FileType);
                            var metadataCreate = await _ediReportService.CreateReceivedFromMetadataAsync(
                                request.ReceiverLibraryId.Value,
                                request.ConnectionLibraryId,
                                item.FileName,
                                metadataType,
                                correlationId,
                                payerName: item.PayerName,
                                paymentAmount: item.PaymentAmount,
                                note: item.Note,
                                traceNumber: item.TraceNumber,
                                cancellationToken: HttpContext.RequestAborted);
                            report = metadataCreate.Report;
                            if (metadataCreate.IsDuplicate) skippedCount++;
                            else processedCount++;
                            _logger.LogInformation(
                                "Inbound file lifecycle status. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash} Status={Status} ClpCount={ClpCount}",
                                correlationId,
                                item.FileName,
                                hash,
                                metadataCreate.IsDuplicate ? "Skipped" : "Processed",
                                metadataCreate.ClpCount ?? 0);
                            createdReports.Add(new
                            {
                                report.Id,
                                report.FileName,
                                report.FileType,
                                report.Status,
                                report.ClaimIdentifier,
                                report.PayerName,
                                report.PaymentAmount,
                                report.Note,
                                isDuplicate = metadataCreate.IsDuplicate
                            });
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            _logger.LogError(
                                ex,
                                "Inbound file failed but pipeline continues. CorrelationId={CorrelationId} FileName={FileName} Reason=HttpInboundFileProcessing",
                                correlationId,
                                item.FileName);
                        }
                    }

                    break;
                }

                case ConnectionType.Sftp:
                {
                    _logger.LogInformation(
                        "EDI inbound SFTP download. CorrelationId={CorrelationId} ConnectionId={ConnectionId} ReceiverId={ReceiverId}",
                        correlationId,
                        connection.Id,
                        request.ReceiverLibraryId);

                    var files = await _sftpTransport.DownloadFilesAsync(connection).ConfigureAwait(false);
                    foreach (var file in files)
                    {
                        try
                        {
                            _logger.LogInformation("Processing file: {FileName}", file.FileName);
                            var fileHash = Zebl.Application.Utilities.ContentHashUtility.Sha256Hex(file.Content);
                            var content = Encoding.UTF8.GetString(file.Content);
                            var fileType = ResolveInboundFileType(file.FileName, content);
                            var create = await _ediReportService.CreateReceivedAsync(
                                request.ReceiverLibraryId.Value,
                                request.ConnectionLibraryId,
                                file.FileName,
                                fileType,
                                file.Content,
                                correlationId,
                                HttpContext.RequestAborted);
                            var report = create.Report;
                            if (create.IsDuplicate) skippedCount++;
                            else processedCount++;

                            _logger.LogInformation(
                                "Inbound file lifecycle status. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash} Status={Status} ClpCount={ClpCount}",
                                correlationId,
                                file.FileName,
                                fileHash,
                                create.IsDuplicate ? "Skipped" : "Processed",
                                create.ClpCount ?? 0);

                            await _sftpTransport.MoveInboundFileAsync(connection, file.FullPath, InboundLifecycleTarget.Processed, HttpContext.RequestAborted)
                                .ConfigureAwait(false);

                            createdReports.Add(new
                            {
                                report.Id,
                                report.FileName,
                                report.FileType,
                                report.Status,
                                report.ClaimIdentifier,
                                report.PayerName,
                                report.PaymentAmount,
                                report.Note,
                                isDuplicate = create.IsDuplicate
                            });
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            var fileHash = Zebl.Application.Utilities.ContentHashUtility.Sha256Hex(file.Content);
                            _logger.LogError(
                                ex,
                                "Inbound file lifecycle status. CorrelationId={CorrelationId} FileName={FileName} Hash={Hash} Status=Failed",
                                correlationId,
                                file.FileName,
                                fileHash);
                            try
                            {
                                await _sftpTransport.MoveInboundFileAsync(connection, file.FullPath, InboundLifecycleTarget.Failed, HttpContext.RequestAborted)
                                    .ConfigureAwait(false);
                            }
                            catch (Exception moveEx)
                            {
                                _logger.LogError(
                                    moveEx,
                                    "Failed moving errored inbound file to failed folder. CorrelationId={CorrelationId} FileName={FileName}",
                                    correlationId,
                                    file.FileName);
                            }
                        }
                    }

                    break;
                }

                default:
                    return Ok(new
                    {
                        processed = 0,
                        skipped = 0,
                        failed = 1,
                        count = 0,
                        skippedCount = 0,
                        failedCount = 1,
                        reports = createdReports,
                        message = "Unsupported ConnectionType.",
                        correlationId
                    });
            }

            var message = processedCount == 0 ? "No new files to process" : null;
            return Ok(new
            {
                processed = processedCount,
                skipped = skippedCount,
                failed = failedCount,
                count = processedCount,
                skippedCount,
                failedCount,
                reports = createdReports,
                message,
                correlationId
            });
        }
        catch (Exception ex)
        {
            failedCount++;
            _logger.LogError(ex, "Inbound download failed at transport/controller level. CorrelationId={CorrelationId}", correlationId);
            return Ok(new
            {
                processed = processedCount,
                skipped = skippedCount,
                failed = failedCount,
                count = processedCount,
                skippedCount,
                failedCount,
                reports = createdReports,
                message = "Download completed with errors.",
                correlationId
            });
        }
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, [FromQuery] bool preview = false)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        try
        {
            await using var stream = await _contentReader.OpenReadAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 64, leaveOpen: true);

            if (preview && report.FileSize > 500 * 1024)
            {
                var buffer = new char[500 * 1024];
                var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                var previewContent = new string(buffer, 0, read);
                return Content($"[FILE TOO LARGE TO PREVIEW ENTIRELY - DOUBLE CLICK FILE TO ANALYZE]\n\n{previewContent}", "text/plain");
            }

            var content = await reader.ReadToEndAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            return Content(content, "text/plain");
        }
        catch (EdiReportFileNotAvailableException ex)
        {
            _logger.LogWarning(ex, "EDI content not available. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
            return NotFound(new { error = ex.Message, correlationId });
        }
    }

    [HttpPost("archive/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();
        await _ediReportService.ArchiveAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("mark-read/{id:guid}")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();
        await _ediReportService.MarkAsReadAsync(id);
        return Ok(new { success = true });
    }

    [HttpPut("{id:guid}/note")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest request)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();
        await _ediReportService.UpdateNoteAsync(id, request?.Note);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();
        await _ediReportService.DeleteAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<IActionResult> Apply835(Guid id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var facilityId = _currentContext.FacilityId;
        var tenantId = _currentContext.TenantId;
        _logger.LogInformation("835 apply request received. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
        try
        {
            var postedBy = User?.Identity?.Name ?? "system";
            _logger.LogInformation("835 apply scope resolved. CorrelationId={CorrelationId} ReportId={ReportId} TenantId={TenantId} FacilityId={FacilityId}", correlationId, id, tenantId, facilityId);
            var result = await _ediAutoPostService.Apply835Async(id, correlationId, postedBy, HttpContext.RequestAborted).ConfigureAwait(false);
            return Ok(new
            {
                processed = result.Processed,
                applied = result.Applied,
                skipped = result.Skipped,
                duplicatesSkipped = result.DuplicatesSkipped,
                unmatched = result.Unmatched,
                reversed = result.Reversed,
                invalid = result.Invalid,
                creditsCreated = result.CreditsCreated,
                correlationId
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "835 apply rejected. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
            return BadRequest(new { error = ex.Message, correlationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "835 apply failed. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
            return StatusCode(500, new { error = "Failed to apply 835 report.", correlationId });
        }
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportFile(Guid id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        try
        {
            var stream = await _contentReader.OpenReadAsync(report, HttpContext.RequestAborted).ConfigureAwait(false);
            return File(stream, "application/octet-stream", report.FileName);
        }
        catch (EdiReportFileNotAvailableException ex)
        {
            _logger.LogWarning(ex, "EDI export failed: file missing. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, id);
            return NotFound(new { error = ex.Message, correlationId });
        }
    }

    private static OutboundEdiKind ResolveOutboundKind(string? fileType, ExportFormat receiverExportFormat)
    {
        var ft = (fileType ?? "").Trim();
        if (string.Equals(ft, "270", StringComparison.OrdinalIgnoreCase))
            return OutboundEdiKind.Eligibility270;
        if (string.Equals(ft, "837", StringComparison.OrdinalIgnoreCase))
            return OutboundEdiKind.Claim837;

        return receiverExportFormat == ExportFormat.Eligibility270
            ? OutboundEdiKind.Eligibility270
            : OutboundEdiKind.Claim837;
    }

    private static string ResolveInboundFileType(string fileName, string content)
    {
        var st01 = X12TransactionDetector.TryGetTransactionIdentifier(content);
        if (X12TransactionDetector.IsSupported(st01))
            return st01!;

        throw new InvalidOperationException(
            $"Cannot determine inbound EDI transaction type from ST01. File={fileName} ST01={st01 ?? "<null>"}.");
    }

    private static string NormalizeInboundTypeFromMetadata(string? fileType)
    {
        var normalized = (fileType ?? string.Empty).Trim();
        if (!X12TransactionDetector.IsSupported(normalized))
            throw new InvalidOperationException($"Inbound metadata fileType {normalized} is unsupported.");
        return normalized;
    }

    public class GenerateEdiReportRequest
    {
        public Guid? ReceiverLibraryId { get; set; }
        public int? ClaimId { get; set; }
        public Guid? ConnectionLibraryId { get; set; }
        public string? FileType { get; set; }
    }

    public class DownloadEdiReportRequest
    {
        public Guid? ConnectionLibraryId { get; set; }
        public Guid? ReceiverLibraryId { get; set; }
    }

    public class UpdateNoteRequest
    {
        public string? Note { get; set; }
    }
}
