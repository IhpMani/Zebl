namespace Zebl.Application.Dtos.Schema;

public class TableColumnDto
{
    public string ColumnName { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public int OrdinalPosition { get; set; }
}
