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
    private readonly EraExceptionService _eraExceptionService;
    private readonly ILogger<EraController> _logger;

    public EraController(IEraPostingService eraPostingService, EraExceptionService eraExceptionService, ILogger<EraController> logger)
    {
        _eraPostingService = eraPostingService;
        _eraExceptionService = eraExceptionService;
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

    [HttpGet("exceptions")]
    public async Task<IActionResult> GetExceptions()
    {
        var list = await _eraExceptionService.GetOpenExceptionsAsync();
        return Ok(list);
    }

    [HttpGet("exceptions/{id:int}")]
    public async Task<IActionResult> GetExceptionById(int id)
    {
        var item = await _eraExceptionService.GetExceptionByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    public sealed class AssignEraExceptionRequest
    {
        public int UserId { get; set; }
    }

    [HttpPost("exceptions/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignEraExceptionRequest request)
    {
        await _eraExceptionService.AssignExceptionAsync(id, request.UserId);
        return NoContent();
    }

    [HttpPost("exceptions/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
    {
        await _eraExceptionService.ResolveExceptionAsync(id);
        return NoContent();
    }
}
