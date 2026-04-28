namespace Zebl.Application.Domain;

public sealed class ClaimPayment
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public int? ClaimId { get; set; }
    public string ClaimExternalId { get; set; } = null!;
    public string TraceNumber { get; set; } = null!;
    public string? PayerId { get; set; }
    public string? PayerLevel { get; set; }
    public decimal PaidAmount { get; set; }

    /// <summary>Insurance amount actually posted to claim/line ledger (may be less than <see cref="PaidAmount"/> when overpaid).</summary>
    public decimal InsuranceAppliedAmount { get; set; }
    public decimal? ChargeAmount { get; set; }
    public decimal? TotalCharge { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public decimal? TakebackAmount { get; set; }
    public decimal? PatientResponsibility { get; set; }
    public string? StatusCode { get; set; }
    public string ServiceLineCode { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public DateTime? CheckDateUtc { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string? PostedBy { get; set; }
    public Guid? SourceReportId { get; set; }
    public Guid? ApplyRunId { get; set; }
    public bool IsReversed { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public long? PaymentBatchId { get; set; }
    public bool IsOrphan { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

