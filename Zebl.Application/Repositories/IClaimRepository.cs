using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IClaimRepository
{
    Task<ClaimData?> GetByIdAsync(int claimId);

    /// <summary>Claim with insureds (primary + secondary) and balance for secondary trigger evaluation.</summary>
    Task<ClaimSecondaryEvalData?> GetClaimForSecondaryEvalAsync(int claimId);

    /// <summary>Adjustments for service lines belonging to this claim. GroupCode, ReasonCode, Amount (positive = reduction).</summary>
    Task<List<(string GroupCode, string? ReasonCode, decimal Amount)>> GetAdjustmentsByClaimIdAsync(int claimId);

    /// <summary>True if a secondary claim already exists for this primary claim.</summary>
    Task<bool> ExistsSecondaryForPrimaryAsync(int primaryClaimId);

    /// <summary>Create secondary claim from primary: copy claim, set type/secondary payer, one insured (seq 2), one service line with charge = forwardAmount. Returns new ClaID.</summary>
    Task<int> CreateSecondaryClaimAsync(int primaryClaimId, int secondaryPayerId, decimal forwardAmount, ClaimSecondaryEvalData fromPrimary);

    /// <summary>
    /// Updates claim submission status after successful 837 export.
    /// </summary>
    Task UpdateSubmissionStatusAsync(int claimId, string submissionMethod, string status, DateTime lastExportedDate);

    /// <summary>
    /// Updates only claim status (e.g. for ERA forwarding: secondary claim status).
    /// </summary>
    Task UpdateClaimStatusAsync(int claimId, string status);

    /// <summary>
    /// Updates claim TRIG totals (from domain service recalc). No SQL aggregation.
    /// </summary>
    Task UpdateTotalsAsync(int claimId, ClaimTotals totals);

    /// <summary>
    /// Get billing physician ID for the claim (for payment creation).
    /// </summary>
    Task<int?> GetBillingPhysicianIdAsync(int claimId);
}
