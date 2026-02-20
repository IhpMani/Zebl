using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly ZeblDbContext _context;

    public ClaimRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimData?> GetByIdAsync(int claimId)
    {
        var claim = await _context.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == claimId);
            
        if (claim == null)
            return null;
            
        return new ClaimData
        {
            ClaimId = claim.ClaID
            // Add other fields as needed for EDI generation
        };
    }
}
