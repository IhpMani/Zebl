using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IClaimRejectionRepository
{
    Task<List<ClaimRejection>> GetAllAsync();

    Task<ClaimRejection?> GetByIdAsync(int id);

    Task AddAsync(ClaimRejection entity);

    Task UpdateAsync(ClaimRejection entity);
}

