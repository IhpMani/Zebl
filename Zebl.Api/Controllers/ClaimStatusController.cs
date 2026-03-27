using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;

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
        var items = ClaimStatusCatalog.All
            .Select(x => new ClaimStatusDto(ClaimStatusCatalog.ToStorage(x.Status), x.DisplayName))
            .ToArray();

        return Ok(items);
    }
}

