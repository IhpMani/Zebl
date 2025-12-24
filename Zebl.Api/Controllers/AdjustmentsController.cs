using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/claims/{claId:int}/adjustments")]
    public class AdjustmentsController : ControllerBase
    {
        private readonly ZeblDbContext _db;

        public AdjustmentsController(ZeblDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAdjustmentsByClaim(int claId)
        {
            // Validate claim exists (cheap check)
            var claimExists = await _db.Claims
                .AsNoTracking()
                .AnyAsync(c => c.ClaID == claId);

            if (!claimExists)
                return NotFound();

            var data = await _db.Adjustments
                .AsNoTracking()
                .Where(a => a.AdjSrv != null && a.AdjSrv.SrvClaFID == claId)
                .OrderByDescending(a => a.AdjID)
                .Select(a => new
                {
                    a.AdjID,
                    a.AdjGroupCode,
                    a.AdjReasonCode,
                    a.AdjAmount,
                    a.AdjDateTimeCreated
                })
                .ToListAsync();

            return Ok(data);
        }
    }
}
