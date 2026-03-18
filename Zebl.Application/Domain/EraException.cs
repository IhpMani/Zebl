namespace Zebl.Application.Domain;

public class EraException
{
    public int Id { get; set; }

    public Guid EdiReportId { get; set; }

    public int? ClaimId { get; set; }

    public int? ServiceLineId { get; set; }

    public string ExceptionType { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string EraClaimIdentifier { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? AssignedUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
