namespace Zebl.Application.Domain;

public class EligibilityRequest
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int PayerId { get; set; }

    public string? PolicyNumber { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? ResponseReceivedAt { get; set; }

    public Guid? EdiReportId { get; set; }
}

