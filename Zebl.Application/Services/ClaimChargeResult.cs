namespace Zebl.Application.Services;

/// <summary>
/// Result of claim charge calculation including overwrite rules for applying library values.
/// </summary>
public class ClaimChargeResult
{
    public decimal Charge { get; set; }
    public decimal Allowed { get; set; }
    public decimal Adjustment { get; set; }
    public bool OverwriteCharge { get; set; }
    public bool OverwriteAllowed { get; set; }
    public bool OverwriteAdjustment { get; set; }
}
