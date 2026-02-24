using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/era")]
[Authorize(Policy = "RequireAuth")]
public class EraController : ControllerBase
{
    private readonly IEraPostingService _eraPostingService;
    private readonly ILogger<EraController> _logger;

    public EraController(IEraPostingService eraPostingService, ILogger<EraController> logger)
    {
        _eraPostingService = eraPostingService;
        _logger = logger;
    }

    /// <summary>
    /// Processes an 835 ERA file: payer match (Payment Matching Key), forwarding logic, auto-post payments.
    /// Controller only calls service; no EDI logic here.
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] EraFile era)
    {
        if (era == null)
            return BadRequest(new { error = "ERA body is required." });
        try
        {
            var result = await _eraPostingService.ProcessEraAsync(era);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ERA");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
