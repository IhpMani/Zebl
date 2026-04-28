namespace Zebl.Application.Edi.Parsing;

public sealed class Edi835ClaimPayment
{
    public string ClaimId { get; init; } = string.Empty;
    public string? StatusCode { get; init; }
    public decimal? TotalCharge { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal? PatientResponsibility { get; init; }
}

