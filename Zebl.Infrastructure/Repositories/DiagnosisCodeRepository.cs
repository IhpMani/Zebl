using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class DiagnosisCodeRepository
{
    private readonly ZeblDbContext _context;

    public DiagnosisCodeRepository(ZeblDbContext context) => _context = context;

    public async Task<(List<Diagnosis_Code> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, bool activeOnly = true, string? codeType = null)
    {
        var query = _context.Diagnosis_Codes.AsNoTracking();
        if (activeOnly)
            query = query.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(codeType))
            query = query.Where(e => e.CodeType == codeType.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s)) || e.CodeType.Contains(s));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(e => e.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<Diagnosis_Code>> LookupAsync(string keyword, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<Diagnosis_Code>();
        var s = keyword.Trim();
        return await _context.Diagnosis_Codes.AsNoTracking()
            .Where(e => e.IsActive && (e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s))))
            .OrderBy(e => e.Code)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Diagnosis_Code?> GetByIdAsync(int id) =>
        await _context.Diagnosis_Codes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Diagnosis_Code?> GetByCodeAsync(string code) =>
        await _context.Diagnosis_Codes.FirstOrDefaultAsync(e => e.Code == code.Trim());

    public async Task<Diagnosis_Code> AddAsync(Diagnosis_Code entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Diagnosis_Codes.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Diagnosis_Code entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Diagnosis_Codes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.Diagnosis_Codes.FindAsync(id);
        if (e != null)
        {
            _context.Diagnosis_Codes.Remove(e);
            await _context.SaveChangesAsync();
        }
    }
}
