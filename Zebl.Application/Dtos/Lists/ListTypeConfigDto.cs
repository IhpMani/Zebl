namespace Zebl.Application.Dtos.Lists;

public class ListTypeConfigDto
{
    public string ListTypeName { get; set; } = null!;
    public string Description { get; set; } = null!;
    /// <summary>Target table (e.g., Claim, Patient). Used when saving form data.</summary>
    public string? TargetTable { get; set; }
    /// <summary>Target column (e.g., ClaClassification, PatClassification). Values from this list update this column.</summary>
    public string? TargetColumn { get; set; }
}
