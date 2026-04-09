namespace Zebl.Infrastructure.Persistence.Entities;

public class InboundIntegration : ITenantEntity, ITenantFacilityEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public bool IsActive { get; set; } = true;
}
