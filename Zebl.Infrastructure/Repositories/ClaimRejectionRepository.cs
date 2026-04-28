using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class ClaimRejectionRepository : IClaimRejectionRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentContext _currentContext;

    public ClaimRejectionRepository(ZeblDbContext context, ICurrentContext currentContext)
    {
        _context = context;
        _currentContext = currentContext;
    }

    public async Task<List<ClaimRejection>> GetAllAsync()
    {
        var tenantId = _currentContext.TenantId;
        var facilityId = _currentContext.FacilityId;

        var scopedClaimIds = _context.Claims
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.FacilityId == facilityId)
            .Select(c => c.ClaID);

        return await _context.ClaimRejections
            .AsNoTracking()
            .Where(r => r.ClaimId.HasValue && scopedClaimIds.Contains(r.ClaimId.Value))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClaimRejection?> GetByIdAsync(int id)
    {
        var tenantId = _currentContext.TenantId;
        var facilityId = _currentContext.FacilityId;

        var scopedClaimIds = _context.Claims
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.FacilityId == facilityId)
            .Select(c => c.ClaID);

        return await _context.ClaimRejections
            .AsNoTracking()
            .Where(r => r.ClaimId.HasValue && scopedClaimIds.Contains(r.ClaimId.Value))
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

