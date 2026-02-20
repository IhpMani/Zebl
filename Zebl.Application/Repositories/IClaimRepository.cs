using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IClaimRepository
{
    Task<ClaimData?> GetByIdAsync(int claimId);
}
