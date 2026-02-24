using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IClaimRepository
{
    Task<ClaimData?> GetByIdAsync(int claimId);

    /// <summary>
    /// Updates claim submission status after successful 837 export.
    /// </summary>
    Task UpdateSubmissionStatusAsync(int claimId, string submissionMethod, string status, DateTime lastExportedDate);

    /// <summary>
    /// Updates only claim status (e.g. for ERA forwarding: secondary claim status).
    /// </summary>
    Task UpdateClaimStatusAsync(int claimId, string status);
}
