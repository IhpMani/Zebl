using Zebl.Application.Dtos.ClaimTemplates;

namespace Zebl.Application.Services;

public interface IClaimTemplateService
{
    Task<List<ClaimTemplateDto>> GetAllAsync();
    Task<ClaimTemplateDto?> GetByIdAsync(int id);
    Task<ClaimTemplateDto> CreateAsync(ClaimTemplateDto dto);
    Task UpdateAsync(int id, ClaimTemplateDto dto);
    Task DeleteAsync(int id);
}

