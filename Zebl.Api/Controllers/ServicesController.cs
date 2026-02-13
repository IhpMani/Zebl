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
            int? claimId = null,
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

            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Service"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            var hasClaStatus = columnsToInclude.Any(c => c.Key == "claStatus");
            var hasClaDateTimeCreated = columnsToInclude.Any(c => c.Key == "claDateTimeCreated");
            var hasPatFirstName = columnsToInclude.Any(c => c.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(c => c.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(c => c.Key == "patFullNameCC");

            var query = _db.Service_Lines.AsNoTracking();

            if (claimId.HasValue)
            {
                query = query.Where(s => s.SrvClaFID == claimId.Value);
            }

            query = query.OrderByDescending(s => s.SrvID);

            var totalCount = await query.CountAsync();

            List<ServiceListItemDto> data;
            if (columnsToInclude.Count > 0 && (hasClaStatus || hasClaDateTimeCreated || hasPatFirstName || hasPatLastName || hasPatFullNameCC))
            {
                var raw = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        s.SrvID,
                        s.SrvClaFID,
                        s.SrvFromDate,
                        s.SrvToDate,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        ClaStatus = s.SrvClaF != null ? s.SrvClaF.ClaStatus : null,
                        ClaDateTimeCreated = s.SrvClaF != null ? s.SrvClaF.ClaDateTimeCreated : default,
                        PatFirstName = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatFirstName : null,
                        PatLastName = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatLastName : null,
                        PatFullNameCC = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatFullNameCC : null
                    })
                    .ToListAsync();

                data = raw.Select(r =>
                {
                    var addCols = new Dictionary<string, object?>();
                    if (hasClaStatus) addCols["claStatus"] = r.ClaStatus;
                    if (hasClaDateTimeCreated) addCols["claDateTimeCreated"] = r.ClaDateTimeCreated;
                    if (hasPatFirstName) addCols["patFirstName"] = r.PatFirstName;
                    if (hasPatLastName) addCols["patLastName"] = r.PatLastName;
                    if (hasPatFullNameCC) addCols["patFullNameCC"] = r.PatFullNameCC;
                    return new ServiceListItemDto
                    {
                        SrvID = r.SrvID,
                        SrvClaFID = r.SrvClaFID,
                        SrvFromDate = r.SrvFromDate,
                        SrvToDate = r.SrvToDate,
                        SrvProcedureCode = r.SrvProcedureCode,
                        SrvDesc = r.SrvDesc,
                        SrvCharges = r.SrvCharges,
                        SrvUnits = r.SrvUnits,
                        SrvTotalBalanceCC = r.SrvTotalBalanceCC,
                        SrvTotalAmtPaidCC = r.SrvTotalAmtPaidCC,
                        AdditionalColumns = addCols
                    };
                }).ToList();
            }
            else
            {
                data = await query
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
            }

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
