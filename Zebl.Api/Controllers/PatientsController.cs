using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Patients;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/patients")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PatientsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(ZeblDbContext db, ILogger<PatientsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPatients(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? searchText = null,
            [FromQuery] bool? active = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? minPatientId = null,
            [FromQuery] int? maxPatientId = null,
            [FromQuery] int? claimId = null,
            [FromQuery] string? additionalColumns = null)
        {
            try
            {
                if (page < 1 || pageSize < 1 || pageSize > 100)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = "Page must be at least 1 and page size must be between 1 and 100"
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

                // Get available column definitions
                var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Patient"];
                var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();

                var query = _db.Patients.AsNoTracking();

                // Active filter
                if (active.HasValue)
                {
                    query = query.Where(p => p.PatActive == active.Value);
                }

                // Date range filter
                if (fromDate.HasValue)
                {
                    query = query.Where(p => p.PatDateTimeCreated >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(p => p.PatDateTimeCreated <= toDate.Value);
                }

                // Patient ID range filter
                if (minPatientId.HasValue)
                {
                    query = query.Where(p => p.PatID >= minPatientId.Value);
                }

                if (maxPatientId.HasValue)
                {
                    query = query.Where(p => p.PatID <= maxPatientId.Value);
                }

                // Claim ID filter - filter patients that have a claim with this ID
                if (claimId.HasValue)
                {
                    query = query.Where(p => p.Claims.Any(c => c.ClaID == claimId.Value));
                }

                // Text search
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLower().Trim();
                    if (int.TryParse(searchText, out int searchInt))
                    {
                        // If it's a number, check both Patient ID and Claim ID
                        query = query.Where(p => p.PatID == searchInt || p.Claims.Any(c => c.ClaID == searchInt));
                    }
                    else
                    {
                        query = query.Where(p =>
                            (p.PatFirstName != null && p.PatFirstName.ToLower().Contains(searchLower)) ||
                            (p.PatLastName != null && p.PatLastName.ToLower().Contains(searchLower)) ||
                            p.PatFullNameCC.ToLower().Contains(searchLower) ||
                            (p.PatAccountNo != null && p.PatAccountNo.ToLower().Contains(searchLower)) ||
                            (p.PatPhoneNo != null && p.PatPhoneNo.Contains(searchText)));
                    }
                }

                // Order by ID (primary key, should be indexed)
                query = query.OrderByDescending(p => p.PatID);

                // Smart count strategy
                int totalCount;
                bool hasFilters = active.HasValue || fromDate.HasValue || toDate.HasValue ||
                                 minPatientId.HasValue || maxPatientId.HasValue || claimId.HasValue || !string.IsNullOrWhiteSpace(searchText);

                if (!hasFilters)
                {
                    try
                    {
                        totalCount = await GetApproxPatientCountAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Approximate count failed, using large estimate");
                        totalCount = 1000000;
                    }
                }
                else
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        totalCount = await query.CountAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Patient count query timed out, using approximate count");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                    catch (SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
                    {
                        _logger.LogWarning(sqlEx, "Patient count query timed out (SQL timeout)");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting patient count");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                }

                List<PatientListItemDto> data;
                try
                {
                    // Add timeout to prevent hanging queries (20 seconds for data query)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var patientData = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(p => new PatientListItemDto
                        {
                            PatID = p.PatID,
                            PatFirstName = p.PatFirstName,
                            PatLastName = p.PatLastName,
                            PatFullNameCC = p.PatFullNameCC,
                            PatDateTimeCreated = p.PatDateTimeCreated,
                            PatActive = p.PatActive,
                            PatAccountNo = p.PatAccountNo,
                            PatBirthDate = p.PatBirthDate,
                            PatPhoneNo = p.PatPhoneNo,
                            PatCity = p.PatCity,
                            PatState = p.PatState,
                            PatTotalBalanceCC = p.PatTotalBalanceCC,
                            AdditionalColumns = new Dictionary<string, object?>()
                        })
                        .ToListAsync(cts.Token);
                    
                    // For now, Patient has no related columns, but we set up the infrastructure
                    // AdditionalColumns dictionary is initialized but empty
                    data = patientData;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Patient data query timed out (cancellation token)");
                    return StatusCode(503, new ErrorResponseDto
                    {
                        ErrorCode = "QUERY_TIMEOUT",
                        Message = "The query took too long to execute. Please try with filters to narrow down the results."
                    });
                }
                catch (SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
                {
                    // SQL Server timeout errors: -2 = Timeout expired, 2 = Timeout during login
                    _logger.LogWarning(sqlEx, "Patient data query timed out (SQL timeout)");
                    return StatusCode(503, new ErrorResponseDto
                    {
                        ErrorCode = "QUERY_TIMEOUT",
                        Message = "The query took too long to execute. Please try with filters to narrow down the results."
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading patient data. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                    return StatusCode(500, new ErrorResponseDto
                    {
                        ErrorCode = "INTERNAL_ERROR",
                        Message = $"An error occurred while loading patient data: {ex.Message}. Please try again or contact support."
                    });
                }

                return Ok(new ApiResponse<List<PatientListItemDto>>
                {
                    Data = data,
                    Meta = new PaginationMetaDto
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetPatients. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "INTERNAL_ERROR",
                    Message = $"An unexpected error occurred: {ex.Message}. Please try again or contact support."
                });
            }
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Patient"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        private async Task<int> GetApproxPatientCountAsync()
        {
            try
            {
                // Fast metadata-based row count (works well for large tables)
                // index_id 0 = heap, 1 = clustered index
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Patient]')
    AND p.index_id IN (0, 1)";

                return await _db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get approximate patient count from metadata, using fallback");
                // Fallback: if sys.* access is restricted, don't break the endpoint
                return 0;
            }
        }
    }
}
