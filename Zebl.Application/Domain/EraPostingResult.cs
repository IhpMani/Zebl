namespace Zebl.Application.Domain;

/// <summary>
/// Result of processing an ERA file. Used by EraPostingService.
/// </summary>
public class EraPostingResult
{
    public bool Success { get; set; }
    /// <summary>True when at least one claim failed (e.g. no payer match) but batch did not crash.</summary>
    public bool PartiallyProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public int PaymentsCreated { get; set; }
    public int ClaimsUpdated { get; set; }

    /// <summary>True when posted balances match 835 amounts (Ins paid + adjustments per line).</summary>
    public bool BalancesMatch { get; set; }
    /// <summary>Per-line verification: expected vs actual deltas. Empty when BalancesMatch is true.</summary>
    public List<string> BalanceVerificationErrors { get; set; } = new();
}
