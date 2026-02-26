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

    public async Task AddAsync(int paymentId, int payerId, int serviceLineId, Guid serviceLineGuid, string groupCode, string? reasonCode, string? remarkCode, decimal amount, decimal reasonAmount)
    {
        if (payerId <= 0) return;
        var now = DateTime.UtcNow;
        var gc = groupCode.Trim().Length > 2 ? groupCode.Trim().Substring(0, 2) : groupCode.Trim();
        var adj = new Adjustment
        {
            AdjPmtFID = paymentId,
            AdjPayFID = payerId,
            AdjSrvFID = serviceLineId,
            AdjSrvGUID = serviceLineGuid,
            AdjTaskFID = serviceLineId,
            AdjGroupCode = gc,
            AdjReasonCode = reasonCode,
            AdjRemarkCode = remarkCode,
            AdjAmount = amount,
            AdjReasonAmount = reasonAmount,
            AdjDateTimeCreated = now,
            AdjDateTimeModified = now
        };
        _context.Adjustments.Add(adj);
        await _context.SaveChangesAsync();
    }

    public async Task<List<(int AdjId, int SrvId, string GroupCode, decimal Amount)>> GetByPaymentIdAsync(int paymentId)
    {
        var list = await _context.Adjustments.AsNoTracking()
            .Where(a => a.AdjPmtFID == paymentId)
            .Select(a => new { a.AdjID, a.AdjSrvFID, a.AdjGroupCode, a.AdjAmount })
            .ToListAsync();
        return list.Select(a => (a.AdjID, a.AdjSrvFID, a.AdjGroupCode, a.AdjAmount)).ToList();
    }

    public async Task DeleteByPaymentIdAsync(int paymentId)
    {
        var list = await _context.Adjustments.Where(a => a.AdjPmtFID == paymentId).ToListAsync();
        _context.Adjustments.RemoveRange(list);
        await _context.SaveChangesAsync();
    }
}
