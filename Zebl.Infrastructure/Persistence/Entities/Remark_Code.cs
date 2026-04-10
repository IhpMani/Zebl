namespace Zebl.Infrastructure.Persistence.Entities;

public class Remark_Code
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
