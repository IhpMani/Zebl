namespace Zebl.Application.Dtos.Schema;

public class ColumnMetadataDto
{
    public string ColumnName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public bool IsForeignKey { get; set; }
    public string? ReferenceTable { get; set; }
    public string? ReferenceDisplayColumn { get; set; }
    public bool IsNullable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsFilterable { get; set; }
    public string Category { get; set; } = "General";
}

public class EntityColumnsResponse
{
    public string EntityName { get; set; } = null!;
    public List<ColumnMetadataDto> Columns { get; set; } = new();
}
