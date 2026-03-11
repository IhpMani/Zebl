namespace Zebl.Application.Dtos.CodeLibrary;

/// <summary>Generic code + description for lookup and list responses.</summary>
public class CodeLibraryItemDto
{
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>Diagnosis code row for grid.</summary>
public class DiagnosisCodeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public string CodeType { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Modifier / POS / Reason / Remark share the same shape.</summary>
public class SimpleCodeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Paged result for any code list.</summary>
public class CodeLibraryPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>CSV import result.</summary>
public class CodeLibraryImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
}
