using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class EdiReportRepository : IEdiReportRepository
{
    private readonly ZeblDbContext _context;

    public EdiReportRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<EdiReport>> GetAllAsync(bool? isArchived = null)
    {
        var query = _context.EdiReports.AsNoTracking().OrderByDescending(r => r.CreatedAt).AsQueryable();
        if (isArchived.HasValue)
            query = query.Where(r => r.IsArchived == isArchived.Value);
        return await query.ToListAsync();
    }

    public async Task<EdiReport?> GetByIdAsync(Guid id)
    {
        return await _context.EdiReports.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> DeleteByReceiverAndConnectionAsync(Guid receiverLibraryId, Guid? connectionLibraryId)
    {
        var toRemove = await _context.EdiReports
            .Where(r => r.ReceiverLibraryId == receiverLibraryId && r.ConnectionLibraryId == connectionLibraryId)
            .ToListAsync();
        _context.EdiReports.RemoveRange(toRemove);
        await _context.SaveChangesAsync();
        return toRemove.Count;
    }

    public async Task AddAsync(EdiReport report)
    {
        await _context.EdiReports.AddAsync(report);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EdiReport report)
    {
        var existing = await _context.EdiReports.FindAsync(report.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(report);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var report = await _context.EdiReports.FindAsync(id);
        if (report != null)
        {
            _context.EdiReports.Remove(report);
            await _context.SaveChangesAsync();
        }
    }
}
