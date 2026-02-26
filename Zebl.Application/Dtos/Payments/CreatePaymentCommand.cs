namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// Command to create a payment and apply to service lines (payment entry).
/// </summary>
public class CreatePaymentCommand
{
    /// <summary>Patient or Payer.</summary>
    public PaymentSourceKind PaymentSource { get; set; }
    public int? PayerId { get; set; }
    public int PatientId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Method { get; set; }
    public string? Reference1 { get; set; }
    public string? Reference2 { get; set; }
    public string? Note { get; set; }
    /// <summary>Optional: billing physician ID. If not set, resolved from first claim.</summary>
    public int? BillingPhysicianId { get; set; }
    /// <summary>When true (e.g. ERA), apply exact amounts from applications without capping to balance.</summary>
    public bool AllowOverApply { get; set; }
    /// <summary>Optional: 835 reference for traceability (e.g. ERA file name). Stored in Pmt835Ref.</summary>
    public string? Ref835 { get; set; }
    public List<ServiceLineApplicationDto> ServiceLineApplications { get; set; } = new();
}

public enum PaymentSourceKind
{
    Patient = 0,
    Payer = 1
}
