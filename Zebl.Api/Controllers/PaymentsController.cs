using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Payments;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/claims/{claId:int}/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly ZeblDbContext _db;

        public PaymentsController(ZeblDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaymentsByClaim(int claId)
        {
            // STEP 1: Resolve PatientId from Claim (explicit + safe)
            var patientId = await _db.Claims
                .AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => c.ClaPatFID)
                .FirstOrDefaultAsync();

            if (patientId == 0)
                return NotFound(); // invalid claim id

            // STEP 2: Get payments for that patient
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
                .ToListAsync();

            return Ok(payments);
        }
    }
}
