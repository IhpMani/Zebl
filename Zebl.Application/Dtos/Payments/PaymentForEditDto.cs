namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// Payment data for the edit form (GET by id).
/// </summary>
public class PaymentForEditDto
{
    public int PaymentId { get; set; }
    public PaymentSourceKind PaymentSource { get; set; }
    public int? PayerId { get; set; }

    /// <summary>First claim found on payment disbursements (for payment entry grid by claim).</summary>
    public int? ClaimId { get; set; }

    public int PatientId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Method { get; set; }
    public string? Reference1 { get; set; }
    public string? Reference2 { get; set; }
    public string? Note { get; set; }
    public decimal? Remaining { get; set; }
}
