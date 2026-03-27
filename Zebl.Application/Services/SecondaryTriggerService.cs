using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Rule-driven secondary claim trigger. Called after ERA is posted and reconciliation passes, or after manual posting.
/// </summary>
public class SecondaryTriggerService : ISecondaryTriggerService
{
    private readonly IClaimRepository _claimRepo;
    private readonly ISecondaryForwardableRulesRepository _rulesRepo;

    public SecondaryTriggerService(
        IClaimRepository claimRepo,
        ISecondaryForwardableRulesRepository rulesRepo)
    {
        _claimRepo = claimRepo;
        _rulesRepo = rulesRepo;
    }

    public async Task<SecondaryTriggerResult> EvaluateAndTriggerAsync(int claimId)
    {
        var result = new SecondaryTriggerResult { Triggered = false, ForwardAmount = 0 };

        var claim = await _claimRepo.GetClaimForSecondaryEvalAsync(claimId);
        if (claim == null)
        {
            result.Reason = "ClaimNotFound";
            return result;
        }

        // PART 8 — Safety: do not create secondary if primary already forwarded to secondary workflow
        if (string.Equals(claim.Status, ClaimStatusCatalog.ToStorage(ClaimStatus.Other), StringComparison.OrdinalIgnoreCase))
        {
            result.Reason = "ClaimAlreadyForwarded";
            return result;
        }

        // PART 2 — No secondary insurance
        if (!claim.SecondaryPayerId.HasValue || claim.SecondaryPayerId.Value <= 0)
        {
            result.Reason = "NoSecondaryInsurance";
            return result;
        }

        // PART 8 — Safety: do not create if claim has open primary balance (primary responsibility still owed)
        if (claim.TotalBalance > 0.001m)
        {
            result.Reason = "OpenPrimaryBalance";
            result.ForwardAmount = 0;
            return result;
        }

        // PART 4 — If claim balance == 0 and no forwardable amount, treat as fully paid
        var adjustments = await _claimRepo.GetAdjustmentsByClaimIdAsync(claimId);
        decimal forwardAmount = 0;
        foreach (var (groupCode, reasonCode, amount) in adjustments)
        {
            var forwardable = await _rulesRepo.IsForwardableAsync(groupCode, reasonCode);
            if (forwardable)
                forwardAmount += Math.Abs(amount);
        }

        result.ForwardAmount = forwardAmount;

        if (forwardAmount <= 0.001m)
        {
            result.Reason = "NoForwardableBalance";
            await _claimRepo.UpdateClaimStatusAsync(claimId, ClaimStatusCatalog.ToStorage(ClaimStatus.Submitted));
            return result;
        }

        if (claim.TotalBalance >= -0.001m && claim.TotalBalance <= 0.001m)
        {
            // Claim balance is zero; forwardable amount is the patient responsibility to send to secondary
        }

        // PART 5 — Secondary already exists?
        if (await _claimRepo.ExistsSecondaryForPrimaryAsync(claimId))
        {
            result.Reason = "SecondaryAlreadyExists";
            return result;
        }

        // Create secondary claim
        try
        {
            var newClaimId = await _claimRepo.CreateSecondaryClaimAsync(claimId, claim.SecondaryPayerId.Value, forwardAmount, claim);
            await _claimRepo.UpdateClaimStatusAsync(claimId, ClaimStatusCatalog.ToStorage(ClaimStatus.Other));
            result.Triggered = true;
            result.Reason = "ForwardedToSecondary";
            result.SecondaryClaimId = newClaimId;
            return result;
        }
        catch (Exception ex)
        {
            result.Reason = "ErrorCreatingSecondary:" + ex.Message;
            return result;
        }
    }
}
