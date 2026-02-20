using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ReceiverLibrary. Uses EF Core, no business logic here.
/// </summary>
public class ReceiverLibraryRepository : IReceiverLibraryRepository
{
    private readonly ZeblDbContext _context;

    public ReceiverLibraryRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<ReceiverLibrary?> GetByIdAsync(Guid id)
    {
        return await _context.ReceiverLibraries
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<ReceiverLibrary>> GetAllAsync()
    {
        return await _context.ReceiverLibraries
            .AsNoTracking()
            .OrderBy(r => r.LibraryEntryName)
            .ToListAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.ReceiverLibraries
            .AnyAsync(r => r.LibraryEntryName == name);
    }

    public async Task AddAsync(ReceiverLibrary entity)
    {
        await _context.ReceiverLibraries.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ReceiverLibrary entity)
    {
        var existing = await _context.ReceiverLibraries.FindAsync(entity.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.ReceiverLibraries.FindAsync(id);
        if (entity != null)
        {
            _context.ReceiverLibraries.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
