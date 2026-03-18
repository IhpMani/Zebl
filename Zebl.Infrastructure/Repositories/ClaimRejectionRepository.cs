using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class ClaimRejectionRepository : IClaimRejectionRepository
{
    private readonly ZeblDbContext _context;

    public ClaimRejectionRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClaimRejection>> GetAllAsync()
    {
        return await _context.ClaimRejections
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClaimRejection?> GetByIdAsync(int id)
    {
        return await _context.ClaimRejections
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task AddAsync(ClaimRejection entity)
    {
        await _context.ClaimRejections.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ClaimRejection entity)
    {
        _context.ClaimRejections.Update(entity);
        await _context.SaveChangesAsync();
    }
}

