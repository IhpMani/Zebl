using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/claim-status")]
[Authorize(Policy = "RequireAuth")]
public class ClaimStatusController : ControllerBase
{
    public record ClaimStatusDto(string Code, string Name);

    [HttpGet]
    public IActionResult GetStatuses()
    {
        // Basic set of statuses; can be extended later or driven from DB/ListValue.
        var items = new[]
        {
            new ClaimStatusDto("NEW", "New"),
            new ClaimStatusDto("ReadyToSubmit", "Ready to Submit"),
            new ClaimStatusDto("Hold", "Hold"),
            new ClaimStatusDto("Billed", "Billed")
        };

        return Ok(items);
    }
}

