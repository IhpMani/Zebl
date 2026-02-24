using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/edi-reports")]
[Authorize(Policy = "RequireAuth")]
public class EdiReportsController : ControllerBase
{
    private readonly EdiReportService _ediReportService;
    private readonly IEdiExportService _ediExportService;
    private readonly IClaimExportService _claimExportService;
    private readonly IConnectionLibraryRepository _connectionRepo;
    private readonly SftpTransportService _sftpTransport;
    private readonly ILogger<EdiReportsController> _logger;

    public EdiReportsController(
        EdiReportService ediReportService,
        IEdiExportService ediExportService,
        IClaimExportService claimExportService,
        IConnectionLibraryRepository connectionRepo,
        SftpTransportService sftpTransport,
        ILogger<EdiReportsController> logger)
    {
        _ediReportService = ediReportService;
        _ediExportService = ediExportService;
        _claimExportService = claimExportService;
        _connectionRepo = connectionRepo;
        _sftpTransport = sftpTransport;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? archived = null)
    {
        try
        {
            var list = await _ediReportService.GetAllAsync(archived);
            // Don't return FileContent in list - too large
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
                r.PayerName,
                r.PaymentAmount,
                r.Note,
                r.IsArchived,
                r.IsRead,
                r.FileSize,
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
        // Don't return FileContent in detail - use /content endpoint
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
            report.PayerName,
            report.PaymentAmount,
            report.Note,
            report.IsArchived,
            report.IsRead,
            report.FileSize,
            report.CreatedAt,
            report.SentAt,
            report.ReceivedAt
        };
        return Ok(dto);
    }

    /// <summary>
    /// Generates 837 for a claim using Payer rules, updates claim status to Submitted, and returns the 837 content.
    /// </summary>
    [HttpPost("claim/{claimId:int}/generate-837")]
    public async Task<IActionResult> Generate837(int claimId)
    {
        try
        {
            var content = await _claimExportService.Generate837Async(claimId);
            return Ok(new { content });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating 837 for claim {ClaimId}", claimId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateEdiReportRequest request)
    {
        if (request?.ReceiverLibraryId == null || request.ClaimId == null)
            return BadRequest(new { error = "ReceiverLibraryId and ClaimId are required." });

        try
        {
            var content = await _ediExportService.GenerateAsync(request.ReceiverLibraryId.Value, request.ClaimId.Value);
            var fileType = request.FileType ?? "837";
            var fileName = $"report_{fileType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.edi";
            var fileContent = Encoding.UTF8.GetBytes(content);

            var report = await _ediReportService.CreateGeneratedAsync(
                request.ReceiverLibraryId.Value,
                request.ConnectionLibraryId,
                fileName,
                fileType,
                fileContent,
                "Outbound");

            return Ok(new
            {
                report.Id,
                report.FileName,
                report.FileType,
                report.Status,
                report.FileSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EDI report");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("send/{id:guid}")]
    public async Task<IActionResult> Send(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        if (report.ConnectionLibraryId == null)
            return BadRequest(new { error = "Report has no connection library assigned." });

        var connection = await _connectionRepo.GetByIdAsync(report.ConnectionLibraryId.Value);
        if (connection == null)
            return BadRequest(new { error = "Connection library not found." });

        if (report.FileContent == null || report.FileContent.Length == 0)
            return BadRequest(new { error = "Generated file content not found. Cannot send." });

        try
        {
            var content = Encoding.UTF8.GetString(report.FileContent);
            await _sftpTransport.UploadFileAsync(connection, report.FileName, content);
            await _ediReportService.MarkSentAsync(id);
            return Ok(new { success = true, message = "File sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending EDI report {Id}", id);
            await _ediReportService.MarkFailedAsync(id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("download")]
    public async Task<IActionResult> Download([FromBody] DownloadEdiReportRequest request)
    {
        if (request?.ConnectionLibraryId == null || request.ReceiverLibraryId == null)
            return BadRequest(new { error = "ConnectionLibraryId and ReceiverLibraryId are required." });

        var connection = await _connectionRepo.GetByIdAsync(request.ConnectionLibraryId.Value);
        if (connection == null)
            return NotFound(new { error = "Connection library not found." });

        try
        {
            var files = await _sftpTransport.DownloadFilesAsync(connection);
            var created = new List<object>();

            foreach (var (fileName, content) in files)
            {
                var fileContentBytes = Encoding.UTF8.GetBytes(content);
                var fileType = InferFileType(fileName, content);
                
                var report = await _ediReportService.CreateReceivedAsync(
                    request.ReceiverLibraryId.Value,
                    request.ConnectionLibraryId,
                    fileName,
                    fileType,
                    fileContentBytes);
                
                created.Add(new
                {
                    report.Id,
                    report.FileName,
                    report.FileType,
                    report.Status,
                    report.PayerName,
                    report.PaymentAmount,
                    report.Note
                });
            }

            return Ok(new { count = created.Count, reports = created });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading EDI reports");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, [FromQuery] bool preview = false)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        if (report.FileContent == null || report.FileContent.Length == 0)
            return NotFound(new { error = "File content not available." });

        // Quick view: show first 500KB only
        if (preview && report.FileSize > 500 * 1024)
        {
            var previewContent = Encoding.UTF8.GetString(report.FileContent, 0, 500 * 1024);
            return Content($"[FILE TOO LARGE TO PREVIEW ENTIRELY - DOUBLE CLICK FILE TO ANALYZE]\n\n{previewContent}", "text/plain");
        }

        var content = Encoding.UTF8.GetString(report.FileContent);
        return Content(content, "text/plain");
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

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportFile(Guid id)
    {
        var report = await _ediReportService.GetByIdAsync(id);
        if (report == null)
            return NotFound();

        if (report.FileContent == null || report.FileContent.Length == 0)
            return NotFound(new { error = "File content not available." });

        return File(report.FileContent, "application/octet-stream", report.FileName);
    }

    /// <summary>
    /// Detects file type from content (ST*835, ST*999, CSR) or falls back to extension.
    /// </summary>
    private static string InferFileType(string fileName, string content)
    {
        // Check content first
        if (content.Contains("ST*835"))
            return "835";
        if (content.Contains("ST*999"))
            return "999";
        if (content.Contains("CSR") || content.Contains("csr"))
            return "CSR";
        
        // Fall back to extension
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (ext == ".edi" || ext == ".835") return "835";
        if (ext == ".837") return "837";
        if (ext == ".270") return "270";
        if (ext == ".999") return "999";
        
        return "835"; // Default
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
