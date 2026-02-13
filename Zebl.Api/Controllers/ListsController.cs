using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Lists;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/lists")]
[Authorize(Policy = "RequireAuth")]
public class ListsController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ILogger<ListsController> _logger;

    // Configuration: Map list type names to entity column queries
    // ONLY the 8 list types specified by user
    private static readonly Dictionary<string, Func<ZeblDbContext, Task<List<ListValueDto>>>> _listTypeQueries = new()
    {
        ["Claim Classification"] = async (db) => 
            await db.Claims
                .Where(c => !string.IsNullOrEmpty(c.ClaClassification))
                .GroupBy(c => c.ClaClassification)
                .Select(g => new ListValueDto { Value = g.Key!, UsageCount = g.Count() })
                .OrderBy(v => v.Value)
                .ToListAsync(),

        ["Patient Classification"] = async (db) =>
            await db.Patients
                .Where(p => !string.IsNullOrEmpty(p.PatClassification))
                .GroupBy(p => p.PatClassification)
                .Select(g => new ListValueDto { Value = g.Key!, UsageCount = g.Count() })
                .OrderBy(v => v.Value)
                .ToListAsync(),

        ["Payer Classification"] = async (db) =>
            await db.Payers
                .Where(p => !string.IsNullOrEmpty(p.PayClassification))
                .GroupBy(p => p.PayClassification)
                .Select(g => new ListValueDto { Value = g.Key!, UsageCount = g.Count() })
                .OrderBy(v => v.Value)
                .ToListAsync(),

        ["Payment Method"] = async (db) =>
            await db.Payments
                .Where(p => !string.IsNullOrEmpty(p.PmtMethod))
                .GroupBy(p => p.PmtMethod)
                .Select(g => new ListValueDto { Value = g.Key!, UsageCount = g.Count() })
                .OrderBy(v => v.Value)
                .ToListAsync(),

        ["Rate Class"] = async (db) =>
            await db.Physicians
                .Where(p => !string.IsNullOrEmpty(p.PhyRateClass))
                .GroupBy(p => p.PhyRateClass)
                .Select(g => new ListValueDto { Value = g.Key!, UsageCount = g.Count() })
                .OrderBy(v => v.Value)
                .ToListAsync(),

        // Document Categories, Note Categories, Statement Global Messages
        // These tables don't exist in DbContext yet, so they return empty lists
        // Values will be managed via ListValue table until tables are added
        ["Document Categories"] = async (db) => await Task.FromResult(new List<ListValueDto>()),
        
        ["Note Categories"] = async (db) => await Task.FromResult(new List<ListValueDto>()),
        
        ["Statement Global Messages"] = async (db) => await Task.FromResult(new List<ListValueDto>()),
    };

    public ListsController(ZeblDbContext db, ILogger<ListsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all available list types
    /// </summary>
    [HttpGet("types")]
    public IActionResult GetListTypes()
    {
        try
        {
            var listTypeConfig = new Dictionary<string, (string Table, string Column)>
            {
                ["Claim Classification"] = ("Claim", "ClaClassification"),
                ["Patient Classification"] = ("Patient", "PatClassification"),
                ["Payer Classification"] = ("Payer", "PayClassification"),
                ["Payment Method"] = ("Payment", "PmtMethod"),
                ["Rate Class"] = ("Physician", "PhyRateClass"),
                ["Document Categories"] = ("", ""),
                ["Note Categories"] = ("", ""),
                ["Statement Global Messages"] = ("", "")
            };

            var listTypes = _listTypeQueries.Keys
                .OrderBy(k => k)
                .Select(k =>
                {
                    var config = listTypeConfig.TryGetValue(k, out var c) ? c : (null!, null!);
                    return new ListTypeConfigDto
                    {
                        ListTypeName = k,
                        Description = k,
                        TargetTable = string.IsNullOrEmpty(config.Table) ? null : config.Table,
                        TargetColumn = string.IsNullOrEmpty(config.Column) ? null : config.Column
                    };
                })
                .ToList();

            return Ok(new ApiResponse<List<ListTypeConfigDto>>
            {
                Data = listTypes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list types");
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to retrieve list types"
            });
        }
    }

    /// <summary>
    /// Get distinct values for a specific list type (from entity columns + custom values)
    /// </summary>
    [HttpGet("values")]
    public async Task<IActionResult> GetListValues([FromQuery] string type)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_PARAMETER",
                    Message = "List type parameter is required"
                });
            }

            if (!_listTypeQueries.ContainsKey(type))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_LIST_TYPE",
                    Message = $"Unknown list type: {type}"
                });
            }

            // Get values from entity columns
            var entityValues = await _listTypeQueries[type](_db);

            // Get custom values from ListValue table
            var customValues = await _db.ListValues
                .Where(lv => lv.ListType == type && lv.IsActive)
                .Select(lv => new ListValueDto { Value = lv.Value, UsageCount = 0 })
                .ToListAsync();

            // Merge and deduplicate
            var allValues = entityValues
                .Concat(customValues)
                .GroupBy(v => v.Value)
                .Select(g => new ListValueDto
                {
                    Value = g.Key,
                    UsageCount = g.Sum(v => v.UsageCount)
                })
                .OrderBy(v => v.Value)
                .ToList();

            return Ok(new ApiResponse<List<ListValueDto>>
            {
                Data = allValues
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list values for type {ListType}", type);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to retrieve list values"
            });
        }
    }

    /// <summary>
    /// Add a new value to a list type
    /// Stores in ListValue table - these values become available in dropdowns
    /// without creating fake entity records
    /// </summary>
    [HttpPost("values")]
    public async Task<IActionResult> AddListValue([FromBody] AddListValueRequest request)
    {
        try
        {
            if (!_listTypeQueries.ContainsKey(request.ListType))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_LIST_TYPE",
                    Message = $"Unknown list type: {request.ListType}"
                });
            }

            // Check if value already exists in entity columns (real data)
            var existingValues = await _listTypeQueries[request.ListType](_db);
            if (existingValues.Any(v => v.Value.Equals(request.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ErrorResponseDto
                {
                    ErrorCode = "VALUE_EXISTS",
                    Message = "This value already exists in actual records"
                });
            }

            // Check if already in ListValue (available values)
            var existingCustom = await _db.ListValues
                .Where(lv => lv.ListType == request.ListType && lv.Value == request.Value)
                .FirstOrDefaultAsync();

            if (existingCustom != null)
            {
                if (!existingCustom.IsActive)
                {
                    // Reactivate previously deleted value
                    existingCustom.IsActive = true;
                    await _db.SaveChangesAsync();
                    
                    return Ok(new ApiResponse<ListValueDto>
                    {
                        Data = new ListValueDto { Value = existingCustom.Value, UsageCount = 0 }
                    });
                }

                return Conflict(new ErrorResponseDto
                {
                    ErrorCode = "VALUE_EXISTS",
                    Message = "This value is already available"
                });
            }

            // Add new value to ListValue table
            // This makes it available in dropdowns without creating fake entity records
            var newValue = new ListValue
            {
                ListType = request.ListType,
                Value = request.Value,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name
            };

            _db.ListValues.Add(newValue);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Added list value {Value} to {ListType} by {User}", 
                request.Value, request.ListType, User.Identity?.Name);

            return Ok(new ApiResponse<ListValueDto>
            {
                Data = new ListValueDto { Value = newValue.Value, UsageCount = 0 }
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UX_ListValue_Type_Value") == true)
        {
            return Conflict(new ErrorResponseDto
            {
                ErrorCode = "VALUE_EXISTS",
                Message = "This value already exists"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding list value {Value} to {ListType}", request.Value, request.ListType);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to add list value"
            });
        }
    }

    /// <summary>
    /// Delete (deactivate) a custom list value
    /// </summary>
    [HttpDelete("values")]
    public async Task<IActionResult> DeleteListValue([FromQuery] string type, [FromQuery] string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_PARAMETER",
                    Message = "Both type and value parameters are required"
                });
            }

            var customValue = await _db.ListValues
                .Where(lv => lv.ListType == type && lv.Value == value && lv.IsActive)
                .FirstOrDefaultAsync();

            if (customValue == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = "Custom value not found or already deleted"
                });
            }

            // Soft delete
            customValue.IsActive = false;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Deleted custom list value {Value} from {ListType} by {User}", 
                value, type, User.Identity?.Name);

            return Ok(new ApiResponse<object>
            {
                Data = new { success = true }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting list value {Value} from {ListType}", value, type);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to delete list value"
            });
        }
    }
}
