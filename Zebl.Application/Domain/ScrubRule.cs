namespace Zebl.Application.Domain;

public class ScrubRule
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Scope { get; set; } = "Claim"; // Claim or ServiceLine

    public string Severity { get; set; } = "Error"; // Error or Warning

    public string Condition { get; set; } = string.Empty;

    public int? PayerId { get; set; }

    public int? ProgramId { get; set; }

    public bool IsActive { get; set; } = true;
}

