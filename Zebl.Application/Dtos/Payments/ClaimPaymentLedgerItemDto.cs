namespace Zebl.Application.Dtos.Payments;

public sealed class ClaimPaymentLedgerItemDto
{
    public long Id { get; set; }
    public int? ClaimId { get; set; }
    public string ClaimExternalId { get; set; } = string.Empty;
    public string TraceNumber { get; set; } = string.Empty;
    public string? PayerId { get; set; }
    public string? PayerLevel { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public decimal? PatientResponsibility { get; set; }
    public bool IsApplied { get; set; }
    public DateTime PaymentDateUtc { get; set; }
}
