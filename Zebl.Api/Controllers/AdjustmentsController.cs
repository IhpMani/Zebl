using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Adjustments;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/adjustments")]
    [Authorize(Policy = "RequireAuth")]
    public class AdjustmentsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<AdjustmentsController> _logger;

        public AdjustmentsController(ZeblDbContext db, ILogger<AdjustmentsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =========================================================
        // 🔴 CLAIM DETAILS ADJUSTMENTS (FAST + SAFE)
        // Claim Details MUST CALL ONLY THIS
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetAdjustmentsForClaim(int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid Claim ID"
                });
            }

            // Pull adjustments via Service → Claim (NO joins loaded)
            var adjustments = await _db.Adjustments
                .AsNoTracking()
                .Where(a => a.AdjSrvF != null && a.AdjSrvF.SrvClaFID == claId)
                .OrderByDescending(a => a.AdjID)
                .Select(a => new AdjustmentListItemDto
                {
                    AdjID = a.AdjID,
                    AdjDateTimeCreated = a.AdjDateTimeCreated,
                    AdjDate = a.AdjDate,
                    AdjAmount = a.AdjAmount,
                    AdjGroupCode = a.AdjGroupCode,
                    AdjReasonCode = a.AdjReasonCode,
                    AdjNote = a.AdjNote,
                    AdjSrvFID = a.AdjSrvFID,
                    AdjPmtFID = a.AdjPmtFID,
                    AdjPayFID = a.AdjPayFID,
                    Adj835Ref = a.Adj835Ref,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .Take(200) // 🔴 HARD LIMIT
                .ToListAsync();

            return Ok(new ApiResponse<List<AdjustmentListItemDto>>
            {
                Data = adjustments
            });
        }

        // =========================================================
        // 🟡 GLOBAL ADJUSTMENTS SEARCH (Find → Adjustments)
        // NOT USED BY CLAIM DETAILS
        // =========================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetAdjustments(
            int page = 1,
            int pageSize = 25,
            int? serviceId = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values"
                });
            }

            var query = _db.Adjustments.AsNoTracking();

            if (serviceId.HasValue)
            {
                query = query.Where(a => a.AdjSrvFID == serviceId.Value);
            }

            query = query.OrderByDescending(a => a.AdjID);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AdjustmentListItemDto
                {
                    AdjID = a.AdjID,
                    AdjDateTimeCreated = a.AdjDateTimeCreated,
                    AdjDate = a.AdjDate,
                    AdjAmount = a.AdjAmount,
                    AdjGroupCode = a.AdjGroupCode,
                    AdjReasonCode = a.AdjReasonCode,
                    AdjNote = a.AdjNote,
                    AdjSrvFID = a.AdjSrvFID,
                    AdjPmtFID = a.AdjPmtFID,
                    AdjPayFID = a.AdjPayFID,
                    Adj835Ref = a.Adj835Ref,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<AdjustmentListItemDto>>
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
        // 🟢 UI CONFIG ONLY
        // =========================================================
        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var columns = RelatedColumnConfig.GetAvailableColumns()["Adjustment"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }
    }
}
