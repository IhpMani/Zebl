using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Physicians;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/physicians")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PhysiciansController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<PhysiciansController> _logger;

        public PhysiciansController(ZeblDbContext db, ILogger<PhysiciansController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPhysicians(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? searchText = null,
            [FromQuery] bool? inactive = null,
            [FromQuery] string? type = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? minPhysicianId = null,
            [FromQuery] int? maxPhysicianId = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page must be at least 1 and page size must be between 1 and 100"
                });
            }

            // Parse additional columns (Physician has no related columns, but we support the parameter for consistency)
            var requestedColumns = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                requestedColumns = additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();
            }

            var query = _db.Physicians.AsNoTracking();

            // Inactive filter
            if (inactive.HasValue)
            {
                query = query.Where(p => p.PhyInactive == inactive.Value);
            }

            // Type filter
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(p => p.PhyType == type);
            }

            // Date range filter
            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PhyDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PhyDateTimeCreated <= toDate.Value);
            }

            // Physician ID range filter
            if (minPhysicianId.HasValue)
            {
                query = query.Where(p => p.PhyID >= minPhysicianId.Value);
            }

            if (maxPhysicianId.HasValue)
            {
                query = query.Where(p => p.PhyID <= maxPhysicianId.Value);
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower().Trim();
                if (int.TryParse(searchText, out int searchInt))
                {
                    query = query.Where(p => p.PhyID == searchInt);
                }
                else
                {
                    query = query.Where(p =>
                        (p.PhyFirstName != null && p.PhyFirstName.ToLower().Contains(searchLower)) ||
                        (p.PhyLastName != null && p.PhyLastName.ToLower().Contains(searchLower)) ||
                        (p.PhyFullNameCC != null && p.PhyFullNameCC.ToLower().Contains(searchLower)) ||
                        (p.PhyNPI != null && p.PhyNPI.Contains(searchText)) ||
                        (p.PhyCity != null && p.PhyCity.ToLower().Contains(searchLower)));
                }
            }

            query = query.OrderByDescending(p => p.PhyID);

            // Smart count strategy
            int totalCount;
            bool hasFilters = inactive.HasValue || !string.IsNullOrWhiteSpace(type) ||
                             fromDate.HasValue || toDate.HasValue ||
                             minPhysicianId.HasValue || maxPhysicianId.HasValue || !string.IsNullOrWhiteSpace(searchText);

            if (!hasFilters)
            {
                try
                {
                    totalCount = await GetApproxPhysicianCountAsync();
                }
                catch
                {
                    totalCount = 10000;
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
                    _logger.LogWarning("Physician count query timed out, using approximate count");
                    try
                    {
                        totalCount = await GetApproxPhysicianCountAsync();
                    }
                    catch
                    {
                        totalCount = pageSize * (page + 10);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting physician count");
                    try
                    {
                        totalCount = await GetApproxPhysicianCountAsync();
                    }
                    catch
                    {
                        totalCount = pageSize * (page + 10);
                    }
                }
            }

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PhysicianListItemDto
                {
                    PhyID = p.PhyID,
                    PhyDateTimeCreated = p.PhyDateTimeCreated,
                    PhyFirstName = p.PhyFirstName,
                    PhyLastName = p.PhyLastName,
                    PhyFullNameCC = p.PhyFullNameCC,
                    PhyNPI = p.PhyNPI,
                    PhyType = p.PhyType,
                    PhyInactive = p.PhyInactive,
                    PhyCity = p.PhyCity,
                    PhyState = p.PhyState,
                    PhyTelephone = p.PhyTelephone,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<PhysicianListItemDto>>
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

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Physician"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        private async Task<int> GetApproxPhysicianCountAsync()
        {
            try
            {
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Physician]')
    AND p.index_id IN (0, 1)";

                return await _db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            }
            catch
            {
                return 0;
            }
        }
    }
}
