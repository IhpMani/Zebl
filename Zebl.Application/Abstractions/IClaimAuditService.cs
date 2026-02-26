namespace Zebl.Application.Abstractions;

/// <summary>
/// Records claim edits in Claim_Audit (notes/history). Call whenever a claim is modified (payment applied, claim updated, etc.).
/// </summary>
public interface IClaimAuditService
{
    /// <summary>
    /// Inserts a Claim_Audit row so the edit appears in claim notes. Use activityType e.g. "Claim Edited", "Payment Applied", "Insurance Edited".
    /// </summary>
    Task RecordClaimEditedAsync(int claimId, string activityType, string notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a "Claim insurance information edited." history row. Call when insurance (Claim_Insured) is updated.
    /// </summary>
    Task AddInsuranceEditedAsync(int claimId, CancellationToken cancellationToken = default);
}
