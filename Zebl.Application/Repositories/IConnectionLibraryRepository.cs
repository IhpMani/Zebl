using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

/// <summary>
/// Repository interface for ConnectionLibrary. Defined in Application layer, implemented in Infrastructure.
/// </summary>
public interface IConnectionLibraryRepository
{
    Task<ConnectionLibrary?> GetByIdAsync(Guid id);
    Task<List<ConnectionLibrary>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string name);
    Task AddAsync(ConnectionLibrary entity);
    Task UpdateAsync(ConnectionLibrary entity);
    Task DeleteAsync(Guid id);
}
