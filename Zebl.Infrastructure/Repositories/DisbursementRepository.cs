using Microsoft.EntityFrameworkCore;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class DisbursementRepository : IDisbursementRepository
{
    private readonly ZeblDbContext _context;

    public DisbursementRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(int paymentId, int serviceLineId, Guid serviceLineGuid, decimal amount, string? note = null)
    {
        var now = DateTime.UtcNow;
        var d = new Disbursement
        {
            DisbPmtFID = paymentId,
            DisbSrvFID = serviceLineId,
            DisbSrvGUID = serviceLineGuid,
            DisbAmount = amount,
            DisbNote = note,
            DisbDateTimeCreated = now,
            DisbDateTimeModified = now
        };
        _context.Disbursements.Add(d);
        await _context.SaveChangesAsync();
    }

    public async Task<List<(int DisbId, int SrvId, decimal Amount)>> GetByPaymentIdAsync(int paymentId)
    {
        var list = await _context.Disbursements.AsNoTracking()
            .Where(x => x.DisbPmtFID == paymentId)
            .Select(x => new { x.DisbID, x.DisbSrvFID, x.DisbAmount })
            .ToListAsync();
        return list.Select(x => (x.DisbID, x.DisbSrvFID, x.DisbAmount)).ToList();
    }

    public async Task DeleteByPaymentIdAsync(int paymentId)
    {
        var list = await _context.Disbursements.Where(x => x.DisbPmtFID == paymentId).ToListAsync();
        _context.Disbursements.RemoveRange(list);
        await _context.SaveChangesAsync();
    }
}
