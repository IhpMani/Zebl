using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Payers;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/payers")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PayersController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<PayersController> _logger;

        public PayersController(ZeblDbContext db, ILogger<PayersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? searchText = null,
            [FromQuery] bool? inactive = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? minPayerId = null,
            [FromQuery] int? maxPayerId = null,
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

            // Parse additional columns (Payer has no related columns, but we support the parameter for consistency)
            var requestedColumns = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                requestedColumns = additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();
            }

            var query = _db.Payers.AsNoTracking();

            // Inactive filter
            if (inactive.HasValue)
            {
                query = query.Where(p => p.PayInactive == inactive.Value);
            }

            // Date range filter
            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PayDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PayDateTimeCreated <= toDate.Value);
            }

            // Payer ID range filter
            if (minPayerId.HasValue)
            {
                query = query.Where(p => p.PayID >= minPayerId.Value);
            }

            if (maxPayerId.HasValue)
            {
                query = query.Where(p => p.PayID <= maxPayerId.Value);
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower().Trim();
                if (int.TryParse(searchText, out int searchInt))
                {
                    query = query.Where(p => p.PayID == searchInt);
                }
                else
                {
                    query = query.Where(p =>
                        (p.PayName != null && p.PayName.ToLower().Contains(searchLower)) ||
                        (p.PayExternalID != null && p.PayExternalID.ToLower().Contains(searchLower)) ||
                        (p.PayCity != null && p.PayCity.ToLower().Contains(searchLower)) ||
                        (p.PayPhoneNo != null && p.PayPhoneNo.Contains(searchText)));
                }
            }

            query = query.OrderByDescending(p => p.PayID);

            // Smart count strategy
            int totalCount;
            bool hasFilters = inactive.HasValue || fromDate.HasValue || toDate.HasValue ||
                             minPayerId.HasValue || maxPayerId.HasValue || !string.IsNullOrWhiteSpace(searchText);

            if (!hasFilters)
            {
                try
                {
                    totalCount = await GetApproxPayerCountAsync();
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
                    _logger.LogWarning("Payer count query timed out, using approximate count");
                    try
                    {
                        totalCount = await GetApproxPayerCountAsync();
                    }
                    catch
                    {
                        totalCount = pageSize * (page + 10);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting payer count");
                    try
                    {
                        totalCount = await GetApproxPayerCountAsync();
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
                .Select(p => new PayerListItemDto
                {
                    PayID = p.PayID,
                    PayDateTimeCreated = p.PayDateTimeCreated,
                    PayName = p.PayName,
                    PayExternalID = p.PayExternalID,
                    PayCity = p.PayCity,
                    PayState = p.PayState,
                    PayPhoneNo = p.PayPhoneNo,
                    PayInactive = p.PayInactive,
                    PayClaimType = p.PayClaimType,
                    PaySubmissionMethod = p.PaySubmissionMethod,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<PayerListItemDto>>
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
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Payer"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        private async Task<int> GetApproxPayerCountAsync()
        {
            try
            {
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Payer]')
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
