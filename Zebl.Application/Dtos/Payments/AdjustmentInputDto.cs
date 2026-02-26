namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// Input for a single adjustment (CO/PR/OA/PI/CR).
/// </summary>
public class AdjustmentInputDto
{
    public string GroupCode { get; set; } = null!;  // CO, PR, OA, PI, CR
    public string? ReasonCode { get; set; }
    public string? RemarkCode { get; set; }
    public decimal Amount { get; set; }
    /// <summary>For PR unbundling: reason-level amount.</summary>
    public decimal ReasonAmount { get; set; }
}
