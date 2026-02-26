using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Rule-driven secondary claim trigger. Call after ERA is posted and reconciliation passes (or after manual posting).
/// </summary>
public interface ISecondaryTriggerService
{
    /// <summary>
    /// Evaluates claim for secondary: has secondary insurance, forwardable PR/CO amount, no existing secondary.
    /// If eligible, creates secondary claim and updates original status. Returns result with reason and forward amount.
    /// </summary>
    Task<SecondaryTriggerResult> EvaluateAndTriggerAsync(int claimId);
}
