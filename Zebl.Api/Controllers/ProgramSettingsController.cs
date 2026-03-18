using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/program-settings")]
[Authorize(Policy = "RequireAuth")]
public class ProgramSettingsController : ControllerBase
{
    private static readonly HashSet<string> EligibilitySourcesRequiringCredentials = new(StringComparer.OrdinalIgnoreCase)
    {
        "Capario", "TriZetto", "Navicure", "PracticeInsight", "ZirMed", "OfficeAlly", "Waystar"
    };

    private readonly ProgramSettingsService _service;
    private readonly ICurrentUserContext _userContext;
    private readonly IEligibilitySettingsProvider? _eligibilitySettingsProvider;

    public ProgramSettingsController(
        ProgramSettingsService service,
        ICurrentUserContext userContext,
        IEligibilitySettingsProvider? eligibilitySettingsProvider = null)
    {
        _service = service;
        _userContext = userContext;
        _eligibilitySettingsProvider = eligibilitySettingsProvider;
    }

    [HttpGet("{section}")]
    public async Task<IActionResult> GetSection(string section, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return BadRequest("Section is required.");
        }

        if (string.Equals(section, "patientEligibility", StringComparison.OrdinalIgnoreCase) && _eligibilitySettingsProvider != null)
        {
            var settings = await _eligibilitySettingsProvider.GetForApiAsync(cancellationToken);
            return Ok(settings);
        }

        var raw = await _service.GetSectionAsync(section, cancellationToken);
        return Ok(raw);
    }

    [HttpPut("{section}")]
    public async Task<IActionResult> SaveSection(string section, [FromBody] JsonElement settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return BadRequest("Section is required.");
        }

        var updatedBy = _userContext.UserName;

        if (string.Equals(section, "patient", StringComparison.OrdinalIgnoreCase))
        {
            var missing = await _service.SavePatientSectionAsync(settings, updatedBy, cancellationToken);
            if (missing.Count > 0)
            {
                return BadRequest(new
                {
                    errorCode = "PATIENTS_MISSING_ACCOUNT_NUMBERS",
                    message = "One or more patients are missing account numbers. Please assign account numbers before enabling this option.",
                    patients = missing
                });
            }

            return NoContent();
        }

        if (string.Equals(section, "patientEligibility", StringComparison.OrdinalIgnoreCase) && _eligibilitySettingsProvider != null)
        {
            var source = settings.TryGetProperty("source", out var src) ? src.GetString()?.Trim() : null;
            if (!string.IsNullOrEmpty(source) && EligibilitySourcesRequiringCredentials.Contains(source))
            {
                var username = settings.TryGetProperty("username", out var u) ? u.GetString()?.Trim() : null;
                if (string.IsNullOrEmpty(username))
                {
                    return BadRequest(new { error = "Username is required when a clearinghouse source is selected." });
                }
            }

            await _eligibilitySettingsProvider.SaveAsync(settings, updatedBy, cancellationToken);
            return NoContent();
        }

        await _service.SaveSectionAsync(section, settings, updatedBy, cancellationToken);

        return NoContent();
    }
}

