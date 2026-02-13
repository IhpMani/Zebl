using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Common;
using System.Collections.Generic;
using Zebl.Application.Dtos.Payments;
using Zebl.Infrastructure.Persistence.Context;

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

            var requestedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                foreach (var k in additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = k.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) requestedColumns.Add(trimmed);
                }
            }

            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Payment"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            var hasPatFirstName = columnsToInclude.Any(c => c.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(c => c.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(c => c.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(c => c.Key == "patAccountNo");

            var query = _db.Payments.AsNoTracking();

            if (patientId.HasValue)
            {
                query = query.Where(p => p.PmtPatFID == patientId.Value);
            }

            query = query.OrderByDescending(p => p.PmtID);

            var totalCount = await query.CountAsync();

            List<PaymentListItemDto> data;
            if (columnsToInclude.Count > 0)
            {
                var raw = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        p.PmtID,
                        p.PmtDateTimeCreated,
                        p.PmtDate,
                        p.PmtAmount,
                        p.PmtPatFID,
                        p.PmtPayFID,
                        p.PmtBFEPFID,
                        p.PmtMethod,
                        p.PmtAuthCode,
                        p.PmtNote,
                        p.Pmt835Ref,
                        p.PmtDisbursedTRIG,
                        p.PmtRemainingCC,
                        PatFirstName = p.PmtPatF != null ? p.PmtPatF.PatFirstName : null,
                        PatLastName = p.PmtPatF != null ? p.PmtPatF.PatLastName : null,
                        PatFullNameCC = p.PmtPatF != null ? p.PmtPatF.PatFullNameCC : null,
                        PatAccountNo = p.PmtPatF != null ? p.PmtPatF.PatAccountNo : null
                    })
                    .ToListAsync();

                data = raw.Select(r =>
                {
                    var addCols = new Dictionary<string, object?>();
                    if (hasPatFirstName) addCols["patFirstName"] = r.PatFirstName;
                    if (hasPatLastName) addCols["patLastName"] = r.PatLastName;
                    if (hasPatFullNameCC) addCols["patFullNameCC"] = r.PatFullNameCC;
                    if (hasPatAccountNo) addCols["patAccountNo"] = r.PatAccountNo;
                    return new PaymentListItemDto
                    {
                        PmtID = r.PmtID,
                        PmtDateTimeCreated = r.PmtDateTimeCreated,
                        PmtDate = r.PmtDate,
                        PmtAmount = r.PmtAmount,
                        PmtPatFID = r.PmtPatFID,
                        PmtPayFID = r.PmtPayFID,
                        PmtBFEPFID = r.PmtBFEPFID,
                        PmtMethod = r.PmtMethod,
                        PmtAuthCode = r.PmtAuthCode,
                        PmtNote = r.PmtNote,
                        Pmt835Ref = r.Pmt835Ref,
                        PmtDisbursedTRIG = r.PmtDisbursedTRIG,
                        PmtRemainingCC = r.PmtRemainingCC,
                        AdditionalColumns = addCols
                    };
                }).ToList();
            }
            else
            {
                data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PaymentListItemDto
                    {
                        PmtID = p.PmtID,
                        PmtDateTimeCreated = p.PmtDateTimeCreated,
                        PmtDate = p.PmtDate,
                        PmtAmount = p.PmtAmount,
                        PmtPatFID = p.PmtPatFID,
                        PmtPayFID = p.PmtPayFID,
                        PmtBFEPFID = p.PmtBFEPFID,
                        PmtMethod = p.PmtMethod,
                        PmtAuthCode = p.PmtAuthCode,
                        PmtNote = p.PmtNote,
                        Pmt835Ref = p.Pmt835Ref,
                        PmtDisbursedTRIG = p.PmtDisbursedTRIG,
                        PmtRemainingCC = p.PmtRemainingCC,
                        AdditionalColumns = new Dictionary<string, object?>()
                    })
                    .ToListAsync();
            }

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
