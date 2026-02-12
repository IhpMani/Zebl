using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Disbursements;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/disbursements")]
    [Authorize(Policy = "RequireAuth")]
    public class DisbursementsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<DisbursementsController> _logger;

        public DisbursementsController(ZeblDbContext db, ILogger<DisbursementsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =========================================================
        // 🔴 CLAIM DETAILS DISBURSEMENTS (FAST + SAFE)
        // Claim Details MUST CALL ONLY THIS
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetDisbursementsForClaim(int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid Claim ID"
                });
            }

            // Resolve patient from claim
            var patientId = await _db.Claims
                .AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => c.ClaPatFID)
                .FirstOrDefaultAsync();

            if (patientId == 0)
                return NotFound();

            // Pull disbursements via payments → patient
            var disbursements = await _db.Disbursements
                .AsNoTracking()
                .Where(d => d.DisbPmtF != null && d.DisbPmtF.PmtPatFID == patientId)
                .OrderByDescending(d => d.DisbID)
                .Select(d => new DisbursementListItemDto
                {
                    DisbID = d.DisbID,
                    DisbDateTimeCreated = d.DisbDateTimeCreated,
                    DisbAmount = d.DisbAmount,
                    DisbPmtFID = d.DisbPmtFID,
                    DisbSrvFID = d.DisbSrvFID,
                    DisbCode = d.DisbCode,
                    DisbNote = d.DisbNote,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .Take(200) // 🔴 HARD LIMIT
                .ToListAsync();

            return Ok(new ApiResponse<List<DisbursementListItemDto>>
            {
                Data = disbursements
            });
        }

        // =========================================================
        // 🟡 GLOBAL DISBURSEMENTS SEARCH (Find → Disbursements)
        // NOT USED BY CLAIM DETAILS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetDisbursements(
            int page = 1,
            int pageSize = 25,
            int? paymentId = null,
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

            var query = _db.Disbursements.AsNoTracking();

            if (paymentId.HasValue)
                query = query.Where(d => d.DisbPmtFID == paymentId.Value);

            if (serviceId.HasValue)
                query = query.Where(d => d.DisbSrvFID == serviceId.Value);

            query = query.OrderByDescending(d => d.DisbID);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DisbursementListItemDto
                {
                    DisbID = d.DisbID,
                    DisbDateTimeCreated = d.DisbDateTimeCreated,
                    DisbAmount = d.DisbAmount,
                    DisbPmtFID = d.DisbPmtFID,
                    DisbSrvFID = d.DisbSrvFID,
                    DisbCode = d.DisbCode,
                    DisbNote = d.DisbNote,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<DisbursementListItemDto>>
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
            var columns = RelatedColumnConfig.GetAvailableColumns()["Disbursement"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }
    }
}
