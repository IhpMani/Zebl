using System;

namespace Zebl.Infrastructure.Persistence.Entities;

public class CustomFieldDefinition
{
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string FieldKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string FieldType { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
