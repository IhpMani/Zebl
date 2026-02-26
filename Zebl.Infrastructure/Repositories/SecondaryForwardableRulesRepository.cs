using Microsoft.EntityFrameworkCore;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class SecondaryForwardableRulesRepository : ISecondaryForwardableRulesRepository
{
    private readonly ZeblDbContext _context;

    public SecondaryForwardableRulesRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsForwardableAsync(string groupCode, string? reasonCode)
    {
        var gc = (groupCode ?? "").Trim().ToUpperInvariant();
        if (gc.Length > 2) gc = gc.Substring(0, 2);
        var rc = (reasonCode ?? "").Trim();
        var rule = await _context.SecondaryForwardableAdjustmentRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.GroupCode == gc && r.ReasonCode == rc);
        return rule?.ForwardToSecondary ?? false;
    }
}
