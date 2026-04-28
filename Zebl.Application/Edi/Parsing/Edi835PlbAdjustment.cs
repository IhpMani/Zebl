namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Provider-level PLB adjustment entry.
/// </summary>
public sealed class Edi835PlbAdjustment
{
    public string? ProviderId { get; init; }
    public DateOnly? FiscalPeriodDate { get; init; }
    public string? AdjustmentIdentifier { get; init; }
    public decimal? Amount { get; init; }
    public int PairIndex { get; init; }
}

