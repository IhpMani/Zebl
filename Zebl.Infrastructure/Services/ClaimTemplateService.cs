using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.ClaimTemplates;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

public class ClaimTemplateService : IClaimTemplateService
{
    private readonly ZeblDbContext _context;

    public ClaimTemplateService(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClaimTemplateDto>> GetAllAsync()
    {
        var items = await _context.ClaimTemplates
            .AsNoTracking()
            .OrderBy(t => t.TemplateName)
            .ToListAsync();

        return items.Select(ToDto).ToList();
    }

    public async Task<ClaimTemplateDto?> GetByIdAsync(int id)
    {
        var e = await _context.ClaimTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return e == null ? null : ToDto(e);
    }

    public async Task<ClaimTemplateDto> CreateAsync(ClaimTemplateDto dto)
    {
        var e = new ClaimTemplate
        {
            TemplateName = dto.TemplateName.Trim(),
            AvailableToPatientId = dto.AvailableToPatientId,
            BillingProviderId = dto.BillingProviderId,
            RenderingProviderId = dto.RenderingProviderId,
            ServiceFacilityId = dto.ServiceFacilityId,
            ReferringProviderId = dto.ReferringProviderId,
            OrderingProviderId = dto.OrderingProviderId,
            SupervisingProviderId = dto.SupervisingProviderId
        };

        _context.ClaimTemplates.Add(e);
        await _context.SaveChangesAsync();
        return ToDto(e);
    }

    public async Task UpdateAsync(int id, ClaimTemplateDto dto)
    {
        var e = await _context.ClaimTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return;

        e.TemplateName = dto.TemplateName.Trim();
        e.AvailableToPatientId = dto.AvailableToPatientId;
        e.BillingProviderId = dto.BillingProviderId;
        e.RenderingProviderId = dto.RenderingProviderId;
        e.ServiceFacilityId = dto.ServiceFacilityId;
        e.ReferringProviderId = dto.ReferringProviderId;
        e.OrderingProviderId = dto.OrderingProviderId;
        e.SupervisingProviderId = dto.SupervisingProviderId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var e = await _context.ClaimTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return;
        _context.ClaimTemplates.Remove(e);
        await _context.SaveChangesAsync();
    }

    private static ClaimTemplateDto ToDto(ClaimTemplate e) => new()
    {
        Id = e.Id,
        TemplateName = e.TemplateName,
        AvailableToPatientId = e.AvailableToPatientId,
        BillingProviderId = e.BillingProviderId,
        RenderingProviderId = e.RenderingProviderId,
        ServiceFacilityId = e.ServiceFacilityId,
        ReferringProviderId = e.ReferringProviderId,
        OrderingProviderId = e.OrderingProviderId,
        SupervisingProviderId = e.SupervisingProviderId
    };
}

