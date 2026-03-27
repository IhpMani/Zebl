using System.Text.Json;
using Zebl.Application.Domain;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Resolves initial Claim.ClaStatus from Program Settings (claim.initialClaimStatus).
/// </summary>
public sealed class ClaimInitialStatusProvider
{
    private readonly ProgramSettingsService _programSettings;

    public ClaimInitialStatusProvider(ProgramSettingsService programSettings)
    {
        _programSettings = programSettings;
    }

    public async Task<string> GetInitialClaStatusStringAsync(CancellationToken cancellationToken = default)
    {
        var section = await _programSettings.GetSectionAsync("claim", cancellationToken);
        if (section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty("initialClaimStatus", out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            var raw = prop.GetString()?.Trim();
            if (!string.IsNullOrEmpty(raw) && ClaimStatusCatalog.TryParse(raw, out var st))
                return ClaimStatusCatalog.ToStorage(st);
        }

        return ClaimStatusCatalog.ToStorage(ClaimStatus.OnHold);
    }
}
