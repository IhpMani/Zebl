using Microsoft.EntityFrameworkCore;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class AdjustmentRepository : IAdjustmentRepository
{
    private readonly ZeblDbContext _context;

    public AdjustmentRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task AddForEraAsync(int paymentId, int payId, int serviceLineId, string groupCode, string? reasonCode, decimal amount)
    {
        var srv = await _context.Service_Lines.AsNoTracking().FirstOrDefaultAsync(s => s.SrvID == serviceLineId);
        if (srv == null) return;
        var now = DateTime.UtcNow;
        var adj = new Adjustment
        {
            AdjPmtFID = paymentId,
            AdjPayFID = payId,
            AdjSrvFID = serviceLineId,
            AdjSrvGUID = srv.SrvGUID,
            AdjTaskFID = serviceLineId,
            AdjGroupCode = groupCode.Length > 2 ? groupCode.Substring(0, 2) : groupCode,
            AdjReasonCode = reasonCode,
            AdjAmount = amount,
            AdjReasonAmount = amount,
            AdjDateTimeCreated = now,
            AdjDateTimeModified = now
        };
        _context.Adjustments.Add(adj);
        await _context.SaveChangesAsync();
    }
}
