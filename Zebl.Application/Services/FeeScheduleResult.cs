namespace Zebl.Application.Services;

/// <summary>
/// Resolved charge, allowed, and adjustment from the fee schedule.
/// </summary>
public class FeeScheduleResult
{
    public decimal Charge { get; set; }
    public decimal Allowed { get; set; }
    public decimal Adjustment { get; set; }
}
