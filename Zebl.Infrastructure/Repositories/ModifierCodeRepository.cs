using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class ModifierCodeRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentUserContext _currentUserContext;

    public ModifierCodeRepository(ZeblDbContext context, ICurrentUserContext currentUserContext)
    {
        _context = context;
        _currentUserContext = currentUserContext;
    }

    private int TenantId => _currentUserContext.TenantId;

    public async Task<(List<Modifier_Code> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var query = _context.Modifier_Codes.AsNoTracking().Where(e => e.TenantId == TenantId);
        if (activeOnly)
            query = query.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s)));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(e => e.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<Modifier_Code>> LookupAsync(string keyword, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<Modifier_Code>();
        var s = keyword.Trim();
        return await _context.Modifier_Codes.AsNoTracking()
            .Where(e => e.TenantId == TenantId && e.IsActive && (e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s))))
            .OrderBy(e => e.Code)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Modifier_Code?> GetByIdAsync(int id) =>
        await _context.Modifier_Codes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId);

    public async Task<Modifier_Code?> GetByCodeAsync(string code) =>
        await _context.Modifier_Codes.FirstOrDefaultAsync(e => e.TenantId == TenantId && e.Code == code.Trim());

    public async Task<Modifier_Code> AddAsync(Modifier_Code entity)
    {
        entity.TenantId = TenantId;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Modifier_Codes.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Modifier_Code entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.TenantId = TenantId;
        _context.Modifier_Codes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.Modifier_Codes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId);
        if (e != null)
        {
            _context.Modifier_Codes.Remove(e);
            await _context.SaveChangesAsync();
        }
    }
}
