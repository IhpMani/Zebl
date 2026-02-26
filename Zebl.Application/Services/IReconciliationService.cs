namespace Zebl.Application.Services;

/// <summary>
/// Verifies claim financial equation: Charge = Paid + Adjustments + Balance. Used after manual or ERA posting.
/// </summary>
public interface IReconciliationService
{
    /// <summary>Verifies claim and all service lines satisfy Charge = Paid + Adjustments + Balance. Returns error message if not.</summary>
    Task<ReconciliationResult> VerifyClaimAsync(int claimId, CancellationToken cancellationToken);
}

public class ReconciliationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }
}
