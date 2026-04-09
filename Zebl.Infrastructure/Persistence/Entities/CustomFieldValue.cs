namespace Zebl.Infrastructure.Persistence.Entities;

public class CustomFieldValue : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string? Value { get; set; }
}
