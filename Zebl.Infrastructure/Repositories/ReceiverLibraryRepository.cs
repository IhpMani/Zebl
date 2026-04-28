using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Abstractions;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ReceiverLibrary. Uses EF Core, no business logic here.
/// </summary>
public class ReceiverLibraryRepository : IReceiverLibraryRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentContext _currentContext;

    public ReceiverLibraryRepository(ZeblDbContext context, ICurrentContext currentContext)
    {
        _context = context;
        _currentContext = currentContext;
    }

    public async Task<ReceiverLibrary?> GetByIdAsync(Guid id)
    {
        return await _context.ReceiverLibraries
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                ((r.TenantId == _currentContext.TenantId && r.FacilityId == _currentContext.FacilityId) ||
                 (!r.TenantId.HasValue && !r.FacilityId.HasValue)));
    }

    public async Task<List<ReceiverLibrary>> GetAllAsync()
    {
        return await _context.ReceiverLibraries
            .Where(r =>
                (r.TenantId == _currentContext.TenantId && r.FacilityId == _currentContext.FacilityId) ||
                (!r.TenantId.HasValue && !r.FacilityId.HasValue))
            .AsNoTracking()
            .OrderBy(r => r.LibraryEntryName)
            .ToListAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.ReceiverLibraries
            .AnyAsync(r =>
                r.LibraryEntryName == name &&
                ((r.TenantId == _currentContext.TenantId && r.FacilityId == _currentContext.FacilityId) ||
                 (!r.TenantId.HasValue && !r.FacilityId.HasValue)));
    }

    public async Task AddAsync(ReceiverLibrary entity)
    {
        await _context.ReceiverLibraries.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ReceiverLibrary entity)
    {
        var existing = await _context.ReceiverLibraries
            .FirstOrDefaultAsync(r =>
                r.Id == entity.Id &&
                ((r.TenantId == _currentContext.TenantId && r.FacilityId == _currentContext.FacilityId) ||
                 (!r.TenantId.HasValue && !r.FacilityId.HasValue)));
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.ReceiverLibraries
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                ((r.TenantId == _currentContext.TenantId && r.FacilityId == _currentContext.FacilityId) ||
                 (!r.TenantId.HasValue && !r.FacilityId.HasValue)));
        if (entity != null)
        {
            _context.ReceiverLibraries.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
