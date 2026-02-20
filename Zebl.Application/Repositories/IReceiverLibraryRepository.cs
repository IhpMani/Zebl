using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

/// <summary>
/// Repository interface for ReceiverLibrary. Defined in Application layer, implemented in Infrastructure.
/// </summary>
public interface IReceiverLibraryRepository
{
    Task<ReceiverLibrary?> GetByIdAsync(Guid id);
    Task<List<ReceiverLibrary>> GetAllAsync();
    Task<bool> ExistsByNameAsync(string name);
    Task AddAsync(ReceiverLibrary entity);
    Task UpdateAsync(ReceiverLibrary entity);
    Task DeleteAsync(Guid id);
}
