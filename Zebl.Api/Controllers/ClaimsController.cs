using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;


namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/claims")]
    public class ClaimsController : ControllerBase
    {
        private readonly ZeblDbContext _db;

        public ClaimsController(ZeblDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetClaims(
        int page = 1,
        int pageSize = 25,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)

        {
            if (page < 1) page = 1;
            if (pageSize > 100) pageSize = 100;

            // Fix: Use the correct DbSet property name 'Claims' instead of 'Claim'
            var query = _db.Claims.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.ClaStatus == status);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(c => c.ClaDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.ClaDateTimeCreated <= toDate.Value);
            }

            query = query.OrderByDescending(c => c.ClaID);


            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ClaimListItemDto
                {
                    ClaID = c.ClaID,
                    ClaStatus = c.ClaStatus,
                    ClaDateTimeCreated = c.ClaDateTimeCreated,
                    ClaTotalChargeTRIG = c.ClaTotalChargeTRIG,
                    ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                    ClaTotalBalanceCC = c.ClaTotalBalanceCC
                })
                .ToListAsync();


            return Ok(new ApiResponse<List<ClaimListItemDto>>
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

        [HttpGet("{claId:int}")]
        public async Task<IActionResult> GetClaimById(int claId)
        {
            var claim = await _db.Claims
                .AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => new
                {
                    // Claim header
                    c.ClaID,
                    c.ClaStatus,
                    c.ClaDateTimeCreated,
                    c.ClaTotalChargeTRIG,
                    c.ClaTotalAmtPaidCC,
                    c.ClaTotalBalanceCC,

                    // Patient (minimal, safe)
                    Patient = new
                    {
                        c.ClaPatF.PatID,
                        c.ClaPatF.PatFirstName,
                        c.ClaPatF.PatLastName,
                        c.ClaPatF.PatBirthDate
                    },

                    // Service lines
                    ServiceLines = c.Service_Lines.Select(s => new
                    {
                        s.SrvID,
                        s.SrvFromDate,
                        s.SrvToDate,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvTotalBalanceCC
                    })
                })
                .FirstOrDefaultAsync();

            if (claim == null)
                return NotFound();

            return Ok(claim);

        }



    }




}


