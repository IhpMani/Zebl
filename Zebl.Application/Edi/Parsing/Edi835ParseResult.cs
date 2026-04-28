namespace Zebl.Application.Edi.Parsing;

public sealed class Edi835ParseResult
{
    public string? PayerName { get; init; }
    public string TraceNumber { get; init; } = "NoTrace";
    public string? TraceTypeCode { get; init; }
    public string? OriginatingCompanyIdentifier { get; init; }

    /// <summary>One entry per CLP loop in document order.</summary>
    public IReadOnlyList<Edi835ClaimGroup> ClaimGroups { get; init; } = Array.Empty<Edi835ClaimGroup>();
    public IReadOnlyList<Edi835ClaimPayment> ClaimPayments { get; init; } = Array.Empty<Edi835ClaimPayment>();

    /// <summary>First CLP claim payment amount (legacy summary field).</summary>
    public decimal? ClaimPaymentAmount { get; init; }

    /// <summary>All CAS rows in document order (denormalized).</summary>
    public IReadOnlyList<Edi835CasAdjustment> CasAdjustments { get; init; } = Array.Empty<Edi835CasAdjustment>();
    public IReadOnlyList<Edi835ServiceLineDetail> ServiceLineDetails { get; init; } = Array.Empty<Edi835ServiceLineDetail>();
    public IReadOnlyList<Edi835PlbAdjustment> ProviderAdjustments { get; init; } = Array.Empty<Edi835PlbAdjustment>();

    public string? SummaryNote { get; init; }

    /// <summary>Payment / check date from BPR16 when present (835 remittance).</summary>
    public DateTime? CheckDateUtc { get; init; }

    /// <summary>Total payment amount from BPR02 when present.</summary>
    public decimal? BprPaymentAmount { get; init; }
}

public sealed class Edi835CasAdjustment
{
    public string? GroupCode { get; init; }
    public string? ReasonCode { get; init; }
    public decimal? Amount { get; init; }
}
