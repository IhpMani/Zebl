using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/eligibility")]
[Authorize(Policy = "RequireAuth")]
public class EligibilityController : ControllerBase
{
    private readonly IEligibilityService _eligibilityService;
    private readonly ILogger<EligibilityController> _logger;

    public EligibilityController(
        IEligibilityService eligibilityService,
        ILogger<EligibilityController> logger)
    {
        _eligibilityService = eligibilityService;
        _logger = logger;
    }

    public sealed class EligibilityRequestBody
    {
        public int PatientId { get; set; }
    }

    public sealed class EligibilityPreflightBody
    {
        public int? PatientId { get; set; }
    }

    [HttpPost("preflight")]
    public async Task<IActionResult> Preflight([FromBody] EligibilityPreflightBody? body, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _eligibilityService.PreflightAsync(
                new EligibilityPreflightRequestDto { PatientId = body?.PatientId },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eligibility preflight failed.");
            return StatusCode(500, new { error = "Preflight validation failed." });
        }
    }

    [HttpPost("request")]
    public async Task<IActionResult> RequestEligibility([FromBody] EligibilityRequestBody request, CancellationToken cancellationToken)
    {
        if (request == null || request.PatientId <= 0)
            return BadRequest(new { error = "PatientId is required." });

        try
        {
            var result = await _eligibilityService.RequestEligibilityAsync(
                new EligibilityRequestCreateDto { PatientId = request.PatientId },
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting eligibility for patient {PatientId}", request.PatientId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, [FromQuery] bool includeRaw271 = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _eligibilityService.GetEligibilityStatusAsync(id, cancellationToken);
            if (item == null)
                return NotFound();

            if (!includeRaw271)
                item.Raw271 = null;

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading eligibility status for request {RequestId}", id);
            return StatusCode(500, new { error = "Failed to load eligibility status." });
        }
    }
}

