using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class AdjustmentRepository : IAdjustmentRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentContext _currentContext;
    private readonly ICurrentUserContext _currentUserContext;

    public AdjustmentRepository(
        ZeblDbContext context,
        ICurrentContext currentContext,
        ICurrentUserContext currentUserContext)
    {
        _context = context;
        _currentContext = currentContext;
        _currentUserContext = currentUserContext;
    }

    public async Task AddForEraAsync(int paymentId, int payId, int serviceLineId, string groupCode, string? reasonCode, decimal amount)
    {
        var userTenantId = _currentContext.TenantId;
        if (userTenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var srv = await _context.Service_Lines.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SrvID == serviceLineId && s.FacilityId == fid);
        if (srv == null) throw new InvalidOperationException("Service line not found.");
        var now = DateTime.UtcNow;
        var adj = new Adjustment
        {
            AdjPmtFID = paymentId,
            AdjPayFID = payId,
            AdjSrvFID = serviceLineId,
            AdjSrvGUID = srv.SrvGUID,
            AdjTaskFID = serviceLineId,
            TenantId = srv.TenantId,
            FacilityId = srv.FacilityId,
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
        var userTenantId = _currentContext.TenantId;
        if (userTenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var srv = await _context.Service_Lines.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SrvID == serviceLineId && s.FacilityId == fid);
        if (srv == null) throw new InvalidOperationException("Service line not found.");
        var now = DateTime.UtcNow;
        var gc = groupCode.Trim().Length > 2 ? groupCode.Trim().Substring(0, 2) : groupCode.Trim();
        var adj = new Adjustment
        {
            AdjPmtFID = paymentId,
            AdjPayFID = payerId,
            AdjSrvFID = serviceLineId,
            AdjSrvGUID = srv.SrvGUID,
            AdjTaskFID = serviceLineId,
            TenantId = srv.TenantId,
            FacilityId = srv.FacilityId,
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
        var fid = _currentContext.FacilityId;
        var list = await _context.Adjustments.AsNoTracking()
            .Where(a => a.AdjPmtFID == paymentId && a.FacilityId == fid)
            .Select(a => new { a.AdjID, a.AdjSrvFID, a.AdjGroupCode, a.AdjAmount })
            .ToListAsync();
        return list.Select(a => (a.AdjID, a.AdjSrvFID, a.AdjGroupCode, a.AdjAmount)).ToList();
    }

    public async Task DeleteByPaymentIdAsync(int paymentId)
    {
        var fid = _currentContext.FacilityId;
        var list = await _context.Adjustments
            .Where(a => a.AdjPmtFID == paymentId && a.FacilityId == fid)
            .ToListAsync();
        _context.Adjustments.RemoveRange(list);
        await _context.SaveChangesAsync();
    }
}
