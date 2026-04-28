using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zebl.Application.Abstractions;

/// <summary>
/// Provides patient eligibility settings. Used by Program Setup API (masked password)
/// and by EligibilityService (decrypted password for clearinghouse only). Credentials
/// are never for application login—only for clearinghouse 270/271 communication.
/// </summary>
public interface IEligibilitySettingsProvider
{
    /// <summary>
    /// Get settings for API response. Password is never returned (masked/empty).
    /// </summary>
    Task<JsonElement> GetForApiAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save settings. Plain password is encrypted before storing. Do not pass through logs or responses.
    /// </summary>
    Task SaveAsync(JsonElement settings, string? updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get settings for eligibility check only (EligibilityService). Includes decrypted password.
    /// Must never be logged or returned to client.
    /// </summary>
    Task<EligibilitySettingsForCheckDto> GetForEligibilityCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal DTO for eligibility check. Password is decrypted; never log or expose.
/// </summary>
public sealed class EligibilitySettingsForCheckDto
{
    public string? ReceiverId { get; set; }
    public string ProviderMode { get; set; } = "Billing";
    public int? SpecificProviderId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public bool ShowEligibilityResponseViewer { get; set; } = true;
}
