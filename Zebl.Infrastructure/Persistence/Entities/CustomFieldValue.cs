namespace Zebl.Infrastructure.Persistence.Entities;

public class CustomFieldValue
{
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string? Value { get; set; }
}
