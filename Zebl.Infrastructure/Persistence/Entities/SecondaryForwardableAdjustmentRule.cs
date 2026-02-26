namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Rule-driven: which adjustment GroupCode+ReasonCode are forwardable to secondary. No hardcoding in service.
/// </summary>
public class SecondaryForwardableAdjustmentRule
{
    public string GroupCode { get; set; } = null!;   // CO, PR, OA, PI, CR (typically PR, sometimes CO)
    public string ReasonCode { get; set; } = null!;  // e.g. 1, 2, 3, 45
    public bool ForwardToSecondary { get; set; }
}
