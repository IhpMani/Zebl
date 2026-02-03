using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/services")]
    [Authorize(Policy = "RequireAuth")]
    public class ServicesController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<ServicesController> _logger;

        public ServicesController(ZeblDbContext db, ILogger<ServicesController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =========================================================
        // 🔴 CLAIM DETAILS — THIS IS THE ONLY ENDPOINT
        // Claim Details MUST call ONLY this
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetServicesForClaim(int claId)
        {
            var services = await _db.Service_Lines
                .AsNoTracking()
                .Where(s => s.SrvClaFID == claId) // 🔴 CRITICAL FILTER
                .OrderBy(s => s.SrvFromDate)
                .Select(s => new ServiceListItemDto
                {
                    SrvID = s.SrvID,
                    SrvClaFID = s.SrvClaFID,
                    SrvFromDate = s.SrvFromDate,
                    SrvToDate = s.SrvToDate,
                    SrvProcedureCode = s.SrvProcedureCode,
                    SrvDesc = s.SrvDesc,
                    SrvCharges = s.SrvCharges,
                    SrvUnits = s.SrvUnits,
                    SrvTotalBalanceCC = s.SrvTotalBalanceCC,
                    SrvTotalAmtPaidCC = s.SrvTotalAmtPaidCC,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .Take(200) // 🔴 HARD SAFETY LIMIT
                .ToListAsync();

            return Ok(new ApiResponse<List<ServiceListItemDto>>
            {
                Data = services
            });
        }

        // =========================================================
        // 🟡 GLOBAL SERVICES SEARCH (NOT USED IN CLAIM DETAILS)
        // Keep this ONLY for Find → Services
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetServices(
            int page = 1,
            int pageSize = 25,
            int? claimId = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values"
                });
            }

            var query = _db.Service_Lines.AsNoTracking();

            if (claimId.HasValue)
            {
                query = query.Where(s => s.SrvClaFID == claimId.Value);
            }

            query = query.OrderByDescending(s => s.SrvID);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new ServiceListItemDto
                {
                    SrvID = s.SrvID,
                    SrvClaFID = s.SrvClaFID,
                    SrvFromDate = s.SrvFromDate,
                    SrvToDate = s.SrvToDate,
                    SrvProcedureCode = s.SrvProcedureCode,
                    SrvDesc = s.SrvDesc,
                    SrvCharges = s.SrvCharges,
                    SrvUnits = s.SrvUnits,
                    SrvTotalBalanceCC = s.SrvTotalBalanceCC,
                    SrvTotalAmtPaidCC = s.SrvTotalAmtPaidCC,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<ServiceListItemDto>>
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

        // =========================================================
        // 🟢 AVAILABLE COLUMNS (UI CONFIG ONLY)
        // =========================================================
        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var columns = RelatedColumnConfig.GetAvailableColumns()["Service"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }
    }
}
