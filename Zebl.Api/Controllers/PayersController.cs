using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Payers;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/payers")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PayersController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ILogger<PayersController> _logger;

        public PayersController(ZeblDbContext db, ILogger<PayersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] bool inactive = false)
        {
            if (page < 1 || pageSize < 1 || pageSize > 5000)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page must be at least 1 and page size must be between 1 and 5000"
                });
            }

            var query = _db.Payers.AsNoTracking();

            // inactive=false → PayInactive = 0 (active only); inactive=true → return all
            if (!inactive)
            {
                query = query.Where(p => p.PayInactive == false);
            }

            query = query.OrderByDescending(p => p.PayID);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PayerListItemDto
                {
                    PayID = p.PayID,
                    PayDateTimeCreated = p.PayDateTimeCreated,
                    PayName = p.PayName,
                    PayClassification = p.PayClassification,
                    PayClaimType = p.PayClaimType,
                    PayExternalID = p.PayExternalID,
                    PayAddr1 = p.PayAddr1,
                    PayCity = p.PayCity,
                    PayState = p.PayState,
                    PayZip = p.PayZip,
                    PayPhoneNo = p.PayPhoneNo,
                    PayEmail = p.PayEmail,
                    PayInactive = p.PayInactive,
                    PaySubmissionMethod = p.PaySubmissionMethod,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new { data, totalCount });
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Payer"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }
    }
}
