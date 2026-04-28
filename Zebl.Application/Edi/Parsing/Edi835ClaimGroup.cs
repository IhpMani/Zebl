namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// One CLP claim payment loop (with nested CAS adjustments collected until the next CLP).
/// </summary>
public sealed class Edi835ClaimGroup
{
    public string? ClaimId { get; init; }
    public string? ClaimStatusCode { get; init; }
    public decimal? TotalClaimChargeAmount { get; init; }
    public decimal? ClaimPaymentAmount { get; init; }
    public decimal? PatientResponsibilityAmount { get; init; }
    public IReadOnlyList<Edi835CasAdjustment> Adjustments { get; init; } = Array.Empty<Edi835CasAdjustment>();
    public IReadOnlyList<Edi835ServiceLineDetail> ServiceLines { get; init; } = Array.Empty<Edi835ServiceLineDetail>();
}
