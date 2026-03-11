namespace Zebl.Infrastructure.Persistence.Entities;

public class Reason_Code
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
