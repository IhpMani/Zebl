namespace Zebl.Application.Services;

/// <summary>
/// Generates 837 for claim submission. Enforces Payer rules; no EDI logic in controllers.
/// </summary>
public interface IClaimExportService
{
    /// <summary>
    /// Generates 837 content for the claim, enforces Payer rules, then updates claim status to Submitted.
    /// </summary>
    Task<string> Generate837Async(int claimId);
}
