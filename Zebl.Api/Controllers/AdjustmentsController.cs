using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
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
        private readonly ICurrentContext _currentContext;
        private readonly ILogger<AdjustmentsController> _logger;

        public AdjustmentsController(ZeblDbContext db, ICurrentContext currentContext, ILogger<AdjustmentsController> logger)
        {
            _db = db;
            _currentContext = currentContext;
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
            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            var adjustments = await _db.Adjustments
                .AsNoTracking()
                .Where(a => a.AdjSrvF != null && a.AdjSrvF.SrvClaFID == claId && a.TenantId == tid && a.FacilityId == fid)
                .OrderByDescending(a => a.AdjID)
                .Select(a => new AdjustmentListItemDto
                {
                    AdjID = a.AdjID,
                    AdjDateTimeCreated = a.AdjDateTimeCreated,
                    AdjDateTimeModified = a.AdjDateTimeModified,
                    CreatedDate = a.AdjDateTimeCreated,
                    ModifiedDate = a.AdjDateTimeModified,
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
            int? serviceId = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values"
                });
            }

            var requestedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                foreach (var k in additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = k.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) requestedColumns.Add(trimmed);
                }
            }

            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Adjustment"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            var hasSrvProcedureCode = columnsToInclude.Any(c => c.Key == "srvProcedureCode");
            var hasSrvDesc = columnsToInclude.Any(c => c.Key == "srvDesc");
            var hasPmtAmount = columnsToInclude.Any(c => c.Key == "pmtAmount");
            var hasPmtDateTimeCreated = columnsToInclude.Any(c => c.Key == "pmtDateTimeCreated");
            var hasPayName = columnsToInclude.Any(c => c.Key == "payName");

            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            var query = _db.Adjustments.AsNoTracking()
                .Where(a => a.TenantId == tid && a.FacilityId == fid);

            if (serviceId.HasValue)
            {
                query = query.Where(a => a.AdjSrvFID == serviceId.Value);
            }

            query = query.OrderByDescending(a => a.AdjID);

            var totalCount = await query.CountAsync();

            List<AdjustmentListItemDto> data;
            if (columnsToInclude.Count > 0)
            {
                var raw = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        a.AdjID,
                        a.AdjDateTimeCreated,
                        a.AdjDateTimeModified,
                        a.AdjDate,
                        a.AdjAmount,
                        a.AdjGroupCode,
                        a.AdjReasonCode,
                        a.AdjNote,
                        a.AdjSrvFID,
                        a.AdjPmtFID,
                        a.AdjPayFID,
                        a.Adj835Ref,
                        SrvProcedureCode = a.AdjSrvF != null ? a.AdjSrvF.SrvProcedureCode : null,
                        SrvDesc = a.AdjSrvF != null ? a.AdjSrvF.SrvDesc : null,
                        PmtAmount = a.AdjPmtF != null ? (decimal?)a.AdjPmtF.PmtAmount : null,
                        PmtDateTimeCreated = a.AdjPmtF != null ? a.AdjPmtF.PmtDateTimeCreated : default,
                        PayName = a.AdjPayF != null ? a.AdjPayF.PayName : null
                    })
                    .ToListAsync();

                data = raw.Select(r =>
                {
                    var addCols = new Dictionary<string, object?>();
                    if (hasSrvProcedureCode) addCols["srvProcedureCode"] = r.SrvProcedureCode;
                    if (hasSrvDesc) addCols["srvDesc"] = r.SrvDesc;
                    if (hasPmtAmount) addCols["pmtAmount"] = r.PmtAmount;
                    if (hasPmtDateTimeCreated) addCols["pmtDateTimeCreated"] = r.PmtDateTimeCreated;
                    if (hasPayName) addCols["payName"] = r.PayName;
                    return new AdjustmentListItemDto
                    {
                        AdjID = r.AdjID,
                        AdjDateTimeCreated = r.AdjDateTimeCreated,
                        AdjDateTimeModified = r.AdjDateTimeModified,
                        CreatedDate = r.AdjDateTimeCreated,
                        ModifiedDate = r.AdjDateTimeModified,
                        AdjDate = r.AdjDate,
                        AdjAmount = r.AdjAmount,
                        AdjGroupCode = r.AdjGroupCode,
                        AdjReasonCode = r.AdjReasonCode,
                        AdjNote = r.AdjNote,
                        AdjSrvFID = r.AdjSrvFID,
                        AdjPmtFID = r.AdjPmtFID,
                        AdjPayFID = r.AdjPayFID,
                        Adj835Ref = r.Adj835Ref,
                        AdditionalColumns = addCols
                    };
                }).ToList();
            }
            else
            {
                data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AdjustmentListItemDto
                    {
                        AdjID = a.AdjID,
                        AdjDateTimeCreated = a.AdjDateTimeCreated,
                        AdjDateTimeModified = a.AdjDateTimeModified,
                        CreatedDate = a.AdjDateTimeCreated,
                        ModifiedDate = a.AdjDateTimeModified,
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
            }

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
