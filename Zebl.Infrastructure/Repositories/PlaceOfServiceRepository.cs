using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class PlaceOfServiceRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentUserContext _currentUserContext;

    public PlaceOfServiceRepository(ZeblDbContext context, ICurrentUserContext currentUserContext)
    {
        _context = context;
        _currentUserContext = currentUserContext;
    }

    private int TenantId => _currentUserContext.TenantId;

    public async Task<(List<Place_of_Service> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var query = _context.Place_of_Services.AsNoTracking().Where(e => e.TenantId == TenantId);
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

    public async Task<List<Place_of_Service>> LookupAsync(string keyword, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<Place_of_Service>();
        var s = keyword.Trim();
        return await _context.Place_of_Services.AsNoTracking()
            .Where(e => e.TenantId == TenantId && e.IsActive && (e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s))))
            .OrderBy(e => e.Code)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Place_of_Service?> GetByIdAsync(int id) =>
        await _context.Place_of_Services.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId);

    public async Task<Place_of_Service?> GetByCodeAsync(string code) =>
        await _context.Place_of_Services.FirstOrDefaultAsync(e => e.TenantId == TenantId && e.Code == code.Trim());

    public async Task<Place_of_Service> AddAsync(Place_of_Service entity)
    {
        entity.TenantId = TenantId;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Place_of_Services.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Place_of_Service entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.TenantId = TenantId;
        _context.Place_of_Services.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.Place_of_Services.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId);
        if (e != null)
        {
            _context.Place_of_Services.Remove(e);
            await _context.SaveChangesAsync();
        }
    }
}
