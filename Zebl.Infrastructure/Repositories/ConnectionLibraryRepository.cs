using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ConnectionLibrary. Uses EF Core, no business logic here.
/// </summary>
public class ConnectionLibraryRepository : IConnectionLibraryRepository
{
    private readonly ZeblDbContext _context;

    public ConnectionLibraryRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<ConnectionLibrary?> GetByIdAsync(Guid id)
    {
        return await _context.ConnectionLibraries
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<ConnectionLibrary>> GetAllAsync()
    {
        return await _context.ConnectionLibraries
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.ConnectionLibraries
            .AnyAsync(c => c.Name == name);
    }

    public async Task AddAsync(ConnectionLibrary entity)
    {
        await _context.ConnectionLibraries.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ConnectionLibrary entity)
    {
        var existing = await _context.ConnectionLibraries.FindAsync(entity.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.ConnectionLibraries.FindAsync(id);
        if (entity != null)
        {
            _context.ConnectionLibraries.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
