namespace Zebl.Infrastructure.Persistence.Entities;

public class Diagnosis_Code
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public string CodeType { get; set; } = null!; // ICD9 or ICD10
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
