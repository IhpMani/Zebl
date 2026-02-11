using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

/// <summary>
/// Controller for interface data review operations
/// Read-only endpoints for viewing import history
/// </summary>
[ApiController]
[Route("api/interface")]
[Authorize(Policy = "RequireAuth")]
public class InterfaceController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ILogger<InterfaceController> _logger;

    public InterfaceController(
        ZeblDbContext db,
        ILogger<InterfaceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets the import history from the database
    /// GET /api/interface/history
    /// Reads directly from SQL table, ordered by timestamp DESC
    /// Returns empty array if table doesn't exist (graceful degradation)
    /// </summary>
    /// <returns>List of import history records</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        try
        {
            _logger.LogInformation("Fetching import history from Hl7_Import_Log table");
            
            // Use a timeout to fail fast if table doesn't exist
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            var history = await _db.Hl7_Import_Logs
                .OrderByDescending(h => h.ImportDateTime)
                .Select(h => new
                {
                    importLogId = h.ImportLogID,
                    fileName = h.FileName,
                    importDateTime = h.ImportDateTime,
                    importSuccessful = h.ImportSuccessful,
                    computerName = h.ComputerName ?? "Unknown",
                    note = h.ImportSuccessful
                        ? $"Imported file '{h.FileName}' from HL7 2.3.1 Import. {h.NewPatientsCount} New Patients, {h.UpdatedPatientsCount} Updated Patients, {h.NewClaimsCount} New Claims Totaling $0.00."
                        : h.ErrorMessage ?? "Import failed"
                })
                .ToListAsync(cts.Token);

            _logger.LogInformation("Retrieved {Count} import history records", history.Count);
            return Ok(history);
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name"))
        {
            // Table doesn't exist - return empty array (graceful degradation)
            _logger.LogWarning("Hl7_Import_Log table does not exist in database (SQL Error {ErrorNumber}: {Message}). Returning empty history.", sqlEx.Number, sqlEx.Message);
            return Ok(new List<object>());
        }
        catch (OperationCanceledException)
        {
            // Timeout - likely table doesn't exist
            _logger.LogWarning("History query timed out. Table may not exist. Returning empty history.");
            return Ok(new List<object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving interface import history: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = $"Error retrieving import history: {ex.Message}" });
        }
    }

    /// <summary>
    /// Diagnostic endpoint to check if Hl7_Import_Log table exists and its structure
    /// GET /api/interface/check-table
    /// </summary>
    [HttpGet("check-table")]
    public async Task<IActionResult> CheckTable()
    {
        try
        {
            // Try to query the table structure
            var tableExists = await _db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Hl7_Import_Log'"
            ).FirstOrDefaultAsync();

            if (tableExists == 0)
            {
                return Ok(new { exists = false, message = "Hl7_Import_Log table does not exist in database" });
            }

            // Check columns
            var columns = await _db.Database.SqlQueryRaw<string>(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Hl7_Import_Log' ORDER BY ORDINAL_POSITION"
            ).ToListAsync();

            var rowCount = await _db.Hl7_Import_Logs.CountAsync();

            return Ok(new
            {
                exists = true,
                rowCount = rowCount,
                columns = columns,
                hasComputerName = columns.Contains("ComputerName", StringComparer.OrdinalIgnoreCase)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking table structure");
            return StatusCode(500, new { error = $"Error checking table: {ex.Message}" });
        }
    }
}
