namespace Zebl.Infrastructure.Persistence.Entities;

public class Tenant
{
    public int TenantId { get; set; }

    /// <summary>Stable URL/header key, lower-case in DB (e.g. nj, mi).</summary>
    public string TenantKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
