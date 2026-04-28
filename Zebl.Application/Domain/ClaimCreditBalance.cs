namespace Zebl.Application.Domain;

public sealed class ClaimCreditBalance
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public int ClaimId { get; set; }
    public Guid SourceReportId { get; set; }
    public string TraceNumber { get; set; } = null!;
    public decimal CreditAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
