namespace Zebl.Application.Domain;

public sealed class PaymentBatch
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public string TraceNumber { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public DateTime CheckDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
}
