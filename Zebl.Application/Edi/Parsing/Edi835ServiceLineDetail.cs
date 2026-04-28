namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Service-line level detail from 835 SVC loop.
/// </summary>
public sealed class Edi835ServiceLineDetail
{
    public string? ProcedureComposite { get; init; }
    public DateOnly? ServiceDate { get; init; }
    public decimal? LineChargeAmount { get; init; }
    public decimal? LinePaidAmount { get; init; }
    public string? RevenueCode { get; init; }
    public IReadOnlyList<Edi835CasAdjustment> Adjustments { get; init; } = Array.Empty<Edi835CasAdjustment>();
}

