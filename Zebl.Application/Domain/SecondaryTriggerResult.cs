namespace Zebl.Application.Domain;

/// <summary>
/// Result of evaluating whether to trigger secondary claim creation.
/// </summary>
public class SecondaryTriggerResult
{
    public bool Triggered { get; set; }
    /// <summary>NoSecondaryInsurance | NoForwardableBalance | FullyPaid | SecondaryAlreadyExists | ForwardedToSecondary | Closed | or error.</summary>
    public string Reason { get; set; } = null!;
    public decimal ForwardAmount { get; set; }
    /// <summary>Set when Triggered is true.</summary>
    public int? SecondaryClaimId { get; set; }
}
