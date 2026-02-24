namespace Zebl.Application.Domain;

/// <summary>
/// Represents a parsed 835 ERA file for auto-posting. Application layer DTO; populated from 835 parse (API or Infrastructure).
/// </summary>
public class EraFile
{
    /// <summary>File or batch identifier for logging.</summary>
    public string FileName { get; set; } = null!;

    /// <summary>Payer identifier from 835 (e.g. N1/NM1 loop) used to match PayExternalID.</summary>
    public string? PayerIdentifier { get; set; }

    /// <summary>BPR total payment amount.</summary>
    public decimal BprTotalAmount { get; set; }

    /// <summary>Check/effective date from 835.</summary>
    public DateOnly CheckDate { get; set; }

    /// <summary>ERA-level status if present (e.g. "Processed as Primary", "Processed as Primary, Forwarded to Additional Payer(s)").</summary>
    public string? EraStatus { get; set; }

    /// <summary>Claims in this ERA.</summary>
    public List<EraClaim> Claims { get; set; } = new();
}

/// <summary>Single claim within an 835 ERA.</summary>
public class EraClaim
{
    /// <summary>Our claim ID when matched (optional; may be matched by patient + ref).</summary>
    public int? ClaimId { get; set; }

    /// <summary>Patient ID when known (for payment and matching).</summary>
    public int? PatientId { get; set; }

    /// <summary>Billing physician ID when known (required for Payment.PmtBFEPFID).</summary>
    public int? BillingPhysicianId { get; set; }

    /// <summary>Claim-level status from 835 (e.g. "Processed as Primary", "Processed as Primary, Forwarded to Additional Payer(s)").</summary>
    public string? ClaimStatus { get; set; }

    /// <summary>Payment amount to post for this claim (from 835). If not set, may use file-level BPR split.</summary>
    public decimal? PaymentAmount { get; set; }

    /// <summary>Service line key (e.g. our SrvID or 835 ref) for applying payment.</summary>
    public List<EraServiceLine> ServiceLines { get; set; } = new();
}

/// <summary>Service line in 835 for payment/adjustment application.</summary>
public class EraServiceLine
{
    public int? ServiceLineId { get; set; }
    public decimal AllowedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    /// <summary>Adjustments: CO, PR, OA, PI, CR with amounts.</summary>
    public List<EraAdjustment> Adjustments { get; set; } = new();
}

/// <summary>Adjustment from 835 (CO, PR, OA, PI, CR).</summary>
public class EraAdjustment
{
    public string GroupCode { get; set; } = null!;  // CO, PR, OA, PI, CR
    public string? ReasonCode { get; set; }
    public decimal Amount { get; set; }
}
