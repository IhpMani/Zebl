using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IEraExceptionRepository
{
    Task<List<EraException>> GetOpenAsync();

    Task<EraException?> GetByIdAsync(int id);

    Task AddAsync(EraException entity);

    Task UpdateAsync(EraException entity);
}

