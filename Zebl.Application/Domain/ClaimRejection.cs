namespace Zebl.Application.Domain;

public class ClaimRejection
{
    public int Id { get; set; }

    public int? ClaimId { get; set; }

    public Guid EdiReportId { get; set; }

    public string ErrorCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Segment { get; set; } = string.Empty;

    public string Element { get; set; } = string.Empty;

    public string Status { get; set; } = "New";

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>Transaction control number from 999 AK2 / 837 ST02, when available.</summary>
    public string? TransactionControlNumber { get; set; }
}

