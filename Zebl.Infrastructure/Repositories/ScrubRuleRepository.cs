using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class ScrubRuleRepository : IScrubRuleRepository
{
    private readonly ZeblDbContext _context;

    public ScrubRuleRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScrubRule>> GetActiveAsync(int? payerId, int? programId)
    {
        var query = _context.ScrubRules.AsNoTracking().Where(r => r.IsActive);

        if (payerId.HasValue)
            query = query.Where(r => r.PayerId == null || r.PayerId == payerId.Value);

        if (programId.HasValue)
            query = query.Where(r => r.ProgramId == null || r.ProgramId == programId.Value);

        return await query.OrderBy(r => r.Name).ToListAsync();
    }
}

