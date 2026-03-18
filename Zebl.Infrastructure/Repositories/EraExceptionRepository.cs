using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class EraExceptionRepository : IEraExceptionRepository
{
    private readonly ZeblDbContext _context;

    public EraExceptionRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<EraException>> GetOpenAsync()
    {
        return await _context.EraExceptions
            .AsNoTracking()
            .Where(e => e.Status == "Open" || e.Status == "InProgress")
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<EraException?> GetByIdAsync(int id)
    {
        return await _context.EraExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task AddAsync(EraException entity)
    {
        await _context.EraExceptions.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EraException entity)
    {
        _context.EraExceptions.Update(entity);
        await _context.SaveChangesAsync();
    }
}

