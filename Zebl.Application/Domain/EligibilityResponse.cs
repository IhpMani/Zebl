namespace Zebl.Application.Domain;

public class EligibilityResponse
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public string Raw271 { get; set; } = string.Empty;

    public string EligibilityStatus { get; set; } = "Unknown";

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
}

