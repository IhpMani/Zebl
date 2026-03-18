namespace Zebl.Application.Services;

public interface IClaimScrubService
{
    Task<IReadOnlyList<ScrubResult>> ScrubClaimAsync(int claimId);
}

public sealed class ScrubResult
{
    public string RuleName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AffectedField { get; set; } = string.Empty;
}

