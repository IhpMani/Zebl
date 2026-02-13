using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

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
    private readonly ZeblDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<Hl7ImportController> _logger;

    public Hl7ImportController(
        Hl7ParserService parser,
        Hl7ImportService importService,
        ZeblDbContext db,
        ICurrentUserContext userContext,
        ILogger<Hl7ImportController> logger)
    {
        _parser = parser;
        _importService = importService;
        _db = db;
        _userContext = userContext;
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
            
            // Process messages and get statistics
            var importResult = await _importService.ProcessHl7Messages(messages, fileName);

            // Insert ONE record per file into Interface_Import_Log (NOT Claim_Audit)
            try
            {
                var importLog = new Interface_Import_Log
                {
                    FileName = fileName,
                    ImportDate = DateTime.UtcNow,
                    UserName = _userContext.UserName ?? "SYSTEM",
                    ComputerName = _userContext.ComputerName,
                    NewPatientsCount = importResult.NewPatientsCount,
                    UpdatedPatientsCount = importResult.UpdatedPatientsCount,
                    NewClaimsCount = importResult.NewClaimsCount,
                    DuplicateClaimsCount = importResult.DuplicateClaimsCount,
                    TotalAmount = importResult.TotalAmount,
                    Notes = importResult.SuccessCount == 0
                        ? "No messages were successfully processed"
                        : $"Imported {importResult.SuccessCount} messages. {importResult.NewPatientsCount} New Patients, {importResult.UpdatedPatientsCount} Updated Patients, {importResult.NewClaimsCount} New Claims, {importResult.DuplicateClaimsCount} Duplicate Claims. Total: ${importResult.TotalAmount:N2}"
                };

                await _db.Interface_Import_Logs.AddAsync(importLog);
                await _db.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx) when (
                dbEx.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
                (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name")))
            {
                _logger.LogWarning("Interface_Import_Log table does not exist. Skipping import log entry.");
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name"))
            {
                _logger.LogWarning("Interface_Import_Log table does not exist. Skipping import log entry.");
            }

            return Ok(new
            {
                success = true,
                fileName = fileName,
                totalMessages = messages.Count,
                successfulMessages = importResult.SuccessCount,
                failedMessages = messages.Count - importResult.SuccessCount,
                newPatients = importResult.NewPatientsCount,
                updatedPatients = importResult.UpdatedPatientsCount,
                newClaims = importResult.NewClaimsCount,
                newServiceLines = importResult.NewServiceLinesCount
            });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx) when (
            dbEx.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name")))
        {
            // Table doesn't exist - this shouldn't happen here, but handle gracefully
            _logger.LogWarning("Database error during import (possibly missing table): {Message}", dbEx.Message);
            return StatusCode(500, new { error = "Database configuration error. Please contact administrator." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing HL7 file {FileName}. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                fileName, ex.GetType().Name, ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = $"Error importing HL7 file: {ex.Message}" });
        }
    }

    /// <summary>
    /// Reviews an HL7 DFT file and returns analysis without committing changes
    /// POST /api/hl7/review
    /// </summary>
    /// <param name="file">The HL7 file to review</param>
    /// <returns>Review result with counts</returns>
    [HttpPost("review")]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ReviewHl7File([FromForm] IFormFile file)
    {
        if (file == null)
        {
            return BadRequest(new { error = "File parameter is required" });
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "File is empty" });
        }

        var fileName = file.FileName ?? "unknown.hl7";

        try
        {
            // Parse HL7 file
            List<Hl7DftMessage> messages;
            using (var fileStream = file.OpenReadStream())
            {
                messages = _parser.ParseHl7File(fileStream, fileName);
            }

            if (messages == null || messages.Count == 0)
            {
                return BadRequest(new { error = "No valid HL7 DFT_P03 messages found in file" });
            }

            // Analyze without committing
            int newPatientsCount = 0;
            int updatedPatientsCount = 0;
            int duplicatePatientsCount = 0;
            int newClaimsCount = 0;
            decimal totalAmount = 0m;

            // Track unique MRNs to detect duplicates within the file
            var seenMrns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mrnsToCheck = new List<string>();
            var messageData = new List<(string mrn, DateOnly? serviceDate, string? visitNumber, decimal amount)>();

            // First pass: extract all MRNs and message data
            foreach (var message in messages)
            {
                if (message == null || string.IsNullOrWhiteSpace(message.PidSegment))
                    continue;

                var mrn = _parser.ExtractPatientMrn(message.PidSegment);
                if (string.IsNullOrWhiteSpace(mrn))
                    continue;

                var normalizedMrn = _parser.NormalizeString(mrn, maxLength: 50);
                if (string.IsNullOrWhiteSpace(normalizedMrn))
                    continue;

                // Check for duplicates within the file
                if (seenMrns.Contains(normalizedMrn))
                {
                    duplicatePatientsCount++;
                    continue; // Skip duplicate patient entries
                }
                seenMrns.Add(normalizedMrn);
                mrnsToCheck.Add(normalizedMrn);

                // Get first service date from FT1 segments
                DateOnly? firstServiceDate = null;
                if (message.Ft1Segments.Count > 0)
                {
                    var firstFt1 = message.Ft1Segments[0];
                    var transactionDateStr = _parser.GetFieldValue(firstFt1, 4);
                    firstServiceDate = _parser.ParseHl7Date(transactionDateStr);
                }

                var visitNumber = !string.IsNullOrWhiteSpace(message.Pv1Segment)
                    ? _parser.NormalizeString(_parser.GetFieldValue(message.Pv1Segment, 19), maxLength: 50)
                    : null;

                // Calculate total amount from FT1 segments
                decimal messageAmount = 0m;
                foreach (var ft1Segment in message.Ft1Segments)
                {
                    var chargesStr = _parser.GetFieldValue(ft1Segment, 10);
                    var charges = _parser.ParseDecimal(chargesStr);
                    messageAmount += charges;
                }
                totalAmount += messageAmount;

                messageData.Add((normalizedMrn, firstServiceDate, visitNumber, messageAmount));
            }

            // Batch query: get all existing patients in one query
            var existingPatients = await _db.Patients
                .Where(p => mrnsToCheck.Contains(p.PatAccountNo))
                .Select(p => new { p.PatAccountNo, p.PatID })
                .ToListAsync();

            var patientLookup = existingPatients.ToDictionary(p => p.PatAccountNo, p => p.PatID);

            // Batch query: get all existing claims for existing patients (simplified)
            var existingPatientIds = patientLookup.Values.Where(id => id > 0).Distinct().ToList();
            var existingClaims = new HashSet<(int patientId, DateOnly serviceDate, string? visitNumber)>();
            
            if (existingPatientIds.Count > 0)
            {
                // Get all claims for these patients - simple batch query
                var allClaims = await _db.Claims
                    .Where(c => existingPatientIds.Contains(c.ClaPatFID))
                    .Select(c => new { c.ClaPatFID, c.ClaFirstDateTRIG, c.ClaMedicalRecordNumber })
                    .ToListAsync();

                foreach (var claim in allClaims)
                {
                    if (claim.ClaFirstDateTRIG.HasValue)
                    {
                        existingClaims.Add((claim.ClaPatFID, claim.ClaFirstDateTRIG.Value, claim.ClaMedicalRecordNumber));
                    }
                }
            }

            // Second pass: analyze using batch-loaded data
            foreach (var (mrn, serviceDate, visitNumber, amount) in messageData)
            {
                if (patientLookup.TryGetValue(mrn, out var patientId))
                {
                    updatedPatientsCount++;
                }
                else
                {
                    newPatientsCount++;
                    patientId = 0; // New patient
                }

                // Count claims (each DFT message = 1 claim)
                bool isNewClaim = true;
                if (patientId > 0 && serviceDate.HasValue)
                {
                    if (existingClaims.Contains((patientId, serviceDate.Value, visitNumber)))
                    {
                        isNewClaim = false;
                    }
                }

                if (isNewClaim)
                {
                    newClaimsCount++;
                }
            }

            return Ok(new
            {
                fileName = fileName,
                interfaceName = "HL7 2.3.1",
                newPatientsCount = newPatientsCount,
                updatedPatientsCount = updatedPatientsCount,
                duplicatePatientsCount = duplicatePatientsCount,
                newClaimsCount = newClaimsCount,
                totalAmount = totalAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing HL7 file {FileName}", fileName);
            return StatusCode(500, new { error = $"Error reviewing HL7 file: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets the import history from Hl7_Import_Log table
    /// GET /api/hl7/history
    /// </summary>
    /// <returns>List of import history records</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetImportHistory()
    {
        try
        {
            var history = await _db.Hl7_Import_Logs
                .OrderByDescending(h => h.ImportDateTime)
                .Select(h => new
                {
                    importLogId = h.ImportLogID,
                    fileName = h.FileName,
                    importDateTime = h.ImportDateTime,
                    importSuccessful = h.ImportSuccessful,
                    note = h.ImportSuccessful
                        ? $"Imported file '{h.FileName}' from HL7 2.3.1 Import. {h.NewPatientsCount} New Patients, {h.UpdatedPatientsCount} Updated Patients, {h.NewClaimsCount} New Claims Totaling $0.00."
                        : h.ErrorMessage ?? "Import failed"
                })
                .ToListAsync();

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HL7 import history");
            return StatusCode(500, new { error = $"Error retrieving import history: {ex.Message}" });
        }
    }
}
