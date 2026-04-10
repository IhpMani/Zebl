using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class DiagnosisCodeRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentUserContext _currentUserContext;

    public DiagnosisCodeRepository(ZeblDbContext context, ICurrentUserContext currentUserContext)
    {
        _context = context;
        _currentUserContext = currentUserContext;
    }

    private int TenantId => _currentUserContext.TenantId;

    public async Task<(List<Diagnosis_Code> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, bool activeOnly = true, string? codeType = null)
    {
        var query = _context.Diagnosis_Codes.AsNoTracking().Where(e => e.TenantId == TenantId);
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
            .Where(e => e.TenantId == TenantId && e.IsActive && (e.Code.Contains(s) || (e.Description != null && e.Description.Contains(s))))
            .OrderBy(e => e.Code)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Diagnosis_Code?> GetByIdAsync(int id) =>
        await _context.Diagnosis_Codes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId);

    public async Task<Diagnosis_Code?> GetByCodeAsync(string code, string codeType) =>
        await _context.Diagnosis_Codes.FirstOrDefaultAsync(e =>
            e.TenantId == TenantId && e.Code == code.Trim() && e.CodeType == codeType.Trim());

    public async Task<Diagnosis_Code> AddAsync(Diagnosis_Code entity)
    {
        entity.TenantId = TenantId;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Diagnosis_Codes.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Diagnosis_Code entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.TenantId = TenantId;
        _context.Diagnosis_Codes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.Diagnosis_Codes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId);
        if (e != null)
        {
            _context.Diagnosis_Codes.Remove(e);
            await _context.SaveChangesAsync();
        }
    }
}
