using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Common;
using System.Collections.Generic;
using Zebl.Application.Dtos.Payments;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize(Policy = "RequireAuth")]
    public class PaymentsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(ZeblDbContext db, ILogger<PaymentsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =========================================================
        // 🔴 CLAIM DETAILS PAYMENTS (FAST + SAFE)
        // Claim Details MUST CALL ONLY THIS
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetPaymentsForClaim(int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid Claim ID"
                });
            }

            // Resolve patient from claim (1 cheap query)
            var patientId = await _db.Claims
                .AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => c.ClaPatFID)
                .FirstOrDefaultAsync();

            if (patientId == 0)
                return NotFound();

            // Pull payments for patient (NO includes, NO joins)
            var payments = await _db.Payments
                .AsNoTracking()
                .Where(p => p.PmtPatFID == patientId)
                .OrderByDescending(p => p.PmtDate)
                .Select(p => new PaymentDto
                {
                    PmtID = p.PmtID,
                    PmtDate = p.PmtDate == default
                        ? (DateTime?)null
                        : p.PmtDate.ToDateTime(TimeOnly.MinValue),
                    PmtAmount = p.PmtAmount,
                    PmtMethod = p.PmtMethod,
                    PmtRemainingCC = p.PmtRemainingCC,
                    PmtNote = p.PmtNote
                })
                .Take(200) // 🔴 HARD LIMIT
                .ToListAsync();

            return Ok(new ApiResponse<List<PaymentDto>>
            {
                Data = payments
            });
        }

        // =========================================================
        // 🟡 GLOBAL PAYMENTS SEARCH (FIND → PAYMENTS)
        // NOT USED BY CLAIM DETAILS
        // =========================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetPayments(
            int page = 1,
            int pageSize = 25,
            int? patientId = null,
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

            IQueryable<Payment> query = _db.Payments.AsNoTracking();
            if (patientId.HasValue && patientId.Value > 0)
                query = query.Where(p => p.PmtPatFID == patientId.Value);
            query = query.OrderByDescending(p => p.PmtDateTimeCreated);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PaymentListItemDto
                {
                    PmtID = p.PmtID,
                    PmtDateTimeCreated = p.PmtDateTimeCreated,
                    PmtDateTimeModified = p.PmtDateTimeModified,
                    PmtCreatedUserName = p.PmtCreatedUserName,
                    PmtLastUserName = p.PmtLastUserName,
                    PmtDate = p.PmtDate,
                    PmtAmount = p.PmtAmount,
                    PmtRemainingCC = p.PmtRemainingCC,
                    PmtChargedPlatformFee = p.PmtChargedPlatformFee,
                    PmtMethod = p.PmtMethod,
                    PmtNote = p.PmtNote,
                    Pmt835Ref = p.Pmt835Ref,
                    PmtOtherReference1 = p.PmtOtherReference1,
                    PmtPatFID = p.PmtPatFID,
                    PmtPayFID = p.PmtPayFID,
                    PmtBFEPFID = p.PmtBFEPFID,
                    PmtAuthCode = p.PmtAuthCode,
                    PmtDisbursedTRIG = p.PmtDisbursedTRIG,
                    PmtPayerName = p.PmtPayF != null ? p.PmtPayF.PayName : null,
                    PayClassification = p.PmtPayF != null ? p.PmtPayF.PayClassification : null,
                    PatAccountNo = p.PmtPatF != null ? p.PmtPatF.PatAccountNo : null,
                    PatLastName = p.PmtPatF != null ? p.PmtPatF.PatLastName : null,
                    PatFirstName = p.PmtPatF != null ? p.PmtPatF.PatFirstName : null,
                    PatFullNameCC = p.PmtPatF != null ? p.PmtPatF.PatFullNameCC : null,
                    PatClassification = p.PmtPatF != null ? p.PmtPatF.PatClassification : null,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<PaymentListItemDto>>
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
            var columns = RelatedColumnConfig.GetAvailableColumns()["Payment"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }
    }
}
