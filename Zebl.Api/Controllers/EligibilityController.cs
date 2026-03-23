using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/eligibility")]
[Authorize(Policy = "RequireAuth")]
public class EligibilityController : ControllerBase
{
    private readonly IEligibilityService _eligibilityService;
    private readonly ZeblDbContext _dbContext;
    private readonly ILogger<EligibilityController> _logger;

    public EligibilityController(
        IEligibilityService eligibilityService,
        ZeblDbContext dbContext,
        ILogger<EligibilityController> logger)
    {
        _eligibilityService = eligibilityService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public sealed class CheckEligibilityRequest
    {
        public int PatientId { get; set; }
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] CheckEligibilityRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.PatientId <= 0)
            return BadRequest(new { error = "PatientId is required." });

        try
        {
            var result = await _eligibilityService.CheckEligibilityAsync(request.PatientId, cancellationToken);
            if (!result.Success)
            {
                _logger.LogWarning(
                    "Eligibility check failed for patient {PatientId}: {Message}",
                    request.PatientId,
                    result.Message);
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running eligibility for patient {PatientId}", request.PatientId);
            return StatusCode(500, new { error = "Failed to run eligibility check." });
        }
    }

    [HttpGet("history/{patientId:int}")]
    public async Task<IActionResult> GetHistory([FromRoute] int patientId, CancellationToken cancellationToken)
    {
        if (patientId <= 0)
            return BadRequest(new { error = "PatientId is required." });

        try
        {
            var items = await _eligibilityService.GetHistoryAsync(patientId, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading eligibility history for patient {PatientId}", patientId);
            return StatusCode(500, new { error = "Failed to load eligibility history." });
        }
    }

    [HttpGet("{requestId:int}/raw")]
    public async Task<IActionResult> GetRawResponse([FromRoute] int requestId, CancellationToken cancellationToken)
    {
        var response = await _dbContext.EligibilityResponses
            .Where(r => r.EligibilityRequestId == requestId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (response == null)
            return NotFound();

        return Ok(new
        {
            requestId,
            raw271 = response.Raw271
        });
    }
}

