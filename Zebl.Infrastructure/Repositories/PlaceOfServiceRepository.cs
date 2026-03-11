using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class PlaceOfServiceRepository
{
    private readonly ZeblDbContext _context;

    public PlaceOfServiceRepository(ZeblDbContext context) => _context = context;

    public async Task<(List<Place_of_Service> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var query = _context.Place_of_Services.AsNoTracking();
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
            .Where(e => e.IsActive && (e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s))))
            .OrderBy(e => e.Code)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Place_of_Service?> GetByIdAsync(int id) =>
        await _context.Place_of_Services.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Place_of_Service?> GetByCodeAsync(string code) =>
        await _context.Place_of_Services.FirstOrDefaultAsync(e => e.Code == code.Trim());

    public async Task<Place_of_Service> AddAsync(Place_of_Service entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Place_of_Services.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Place_of_Service entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Place_of_Services.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.Place_of_Services.FindAsync(id);
        if (e != null)
        {
            _context.Place_of_Services.Remove(e);
            await _context.SaveChangesAsync();
        }
    }
}
