namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Core scoped entities that require an explicit non-zero <see cref="TenantId"/> on insert
/// and must not have <see cref="TenantId"/> changed on update.
/// </summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}
