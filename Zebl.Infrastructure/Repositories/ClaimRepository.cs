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
        };
    }

    public async Task UpdateSubmissionStatusAsync(int claimId, string submissionMethod, string status, DateTime lastExportedDate)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null) return;
        claim.ClaSubmissionMethod = submissionMethod;
        claim.ClaStatus = status;
        claim.ClaLastExportedDate = DateOnly.FromDateTime(lastExportedDate);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateClaimStatusAsync(int claimId, string status)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null) return;
        claim.ClaStatus = status ?? claim.ClaStatus;
        await _context.SaveChangesAsync();
    }
}
