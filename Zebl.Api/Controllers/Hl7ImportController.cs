using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Api.Services;

namespace Zebl.Api.Controllers;

/// <summary>
/// Controller for importing HL7 DFT (Detail Financial Transaction) files
/// </summary>
[ApiController]
[Route("api/hl7")]
[Authorize(Policy = "RequireAuth")]
public class Hl7ImportController : ControllerBase
{
    private readonly Hl7ParserService _parser;
    private readonly Hl7ImportService _importService;
    private readonly ILogger<Hl7ImportController> _logger;

    public Hl7ImportController(
        Hl7ParserService parser,
        Hl7ImportService importService,
        ILogger<Hl7ImportController> logger)
    {
        _parser = parser;
        _importService = importService;
        _logger = logger;
    }

    /// <summary>
    /// Imports an HL7 DFT file and creates Patients, Claims, and Service_Lines
    /// POST /api/hl7/import
    /// </summary>
    /// <param name="file">The HL7 file to import</param>
    /// <returns>Import result with counts</returns>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)] // Hide from Swagger to avoid IFormFile/[FromForm] Swagger bug
    public async Task<IActionResult> ImportHl7File([FromForm] IFormFile file)
    {
        if (file == null)
        {
            _logger.LogWarning("HL7 import request: file parameter is null");
            return BadRequest(new { error = "File parameter is required" });
        }

        if (file.Length == 0)
        {
            _logger.LogWarning("HL7 import request: file is empty. FileName: {FileName}", file.FileName);
            return BadRequest(new { error = "File is empty" });
        }

        var fileName = file.FileName ?? "unknown.hl7";

        try
        {
            // Parse HL7 file
            List<Hl7DftMessage> messages;
            using (var fileStream = file.OpenReadStream())
            {
                _logger.LogInformation("Parsing HL7 DFT file: {FileName}, Size: {Size} bytes", fileName, file.Length);
                messages = _parser.ParseHl7File(fileStream, fileName);
            }

            if (messages == null || messages.Count == 0)
            {
                _logger.LogWarning("No valid HL7 DFT_P03 messages found in file {FileName}", fileName);
                return BadRequest(new { error = "No valid HL7 DFT_P03 messages found in file" });
            }

            // Process messages and create database entities
            _logger.LogInformation("Processing {Count} HL7 DFT messages from file {FileName}", messages.Count, fileName);
            var successCount = await _importService.ProcessHl7Messages(messages);

            return Ok(new
            {
                success = true,
                fileName = fileName,
                totalMessages = messages.Count,
                successfulMessages = successCount,
                failedMessages = messages.Count - successCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing HL7 file {FileName}. Exception: {ExceptionType}, Message: {Message}", 
                fileName, ex.GetType().Name, ex.Message);
            return StatusCode(500, new { error = $"Error importing HL7 file: {ex.Message}" });
        }
    }
}
