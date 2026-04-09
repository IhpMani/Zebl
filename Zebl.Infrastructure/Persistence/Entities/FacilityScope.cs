namespace Zebl.Infrastructure.Persistence.Entities;

public class FacilityScope
{
    public int FacilityId { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
