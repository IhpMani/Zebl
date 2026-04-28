using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/program-settings")]
[Authorize(Policy = "RequireAuth")]
public class ProgramSettingsController : ControllerBase
{
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
        if (string.Equals(section, "claim", StringComparison.OrdinalIgnoreCase))
            return Ok(NormalizeClaimSectionSettings(raw));
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
            var receiverId = settings.TryGetProperty("receiverId", out var receiverIdNode) ? receiverIdNode.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(receiverId) || !Guid.TryParse(receiverId, out _))
            {
                return BadRequest(new { error = "Receiver is required for eligibility." });
            }

            var username = settings.TryGetProperty("username", out var u) ? u.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required for eligibility." });
            }

            var server = settings.TryGetProperty("server", out var s) ? s.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(server))
            {
                return BadRequest(new { error = "Server is required for eligibility." });
            }

            await _eligibilitySettingsProvider.SaveAsync(settings, updatedBy, cancellationToken);
            return NoContent();
        }

        if (string.Equals(section, "claim", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("PROGRAM SETTINGS (claim) incoming payload: " + settings.GetRawText());
            settings = NormalizeClaimSectionSettings(settings);
            Console.WriteLine("PROGRAM SETTINGS (claim) normalized payload: " + settings.GetRawText());
        }

        await _service.SaveSectionAsync(section, settings, updatedBy, cancellationToken);

        if (string.Equals(section, "claim", StringComparison.OrdinalIgnoreCase))
        {
            var persisted = await _service.GetSectionAsync("claim", cancellationToken);
            Console.WriteLine("PROGRAM SETTINGS (claim) persisted payload: " + persisted.GetRawText());
        }

        return NoContent();
    }

    private static JsonElement NormalizeClaimSectionSettings(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
            return settings;

        var node = JsonNode.Parse(settings.GetRawText());
        if (node is not JsonObject obj)
            return settings;

        var raw = obj["initialClaimStatus"]?.GetValue<string>()?.Trim();
        if (!ClaimStatusCatalog.TryParse(raw, out var st))
            st = ClaimStatus.OnHold;
        obj["initialClaimStatus"] = ClaimStatusCatalog.ToStorage(st);

        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return doc.RootElement.Clone();
    }
}

