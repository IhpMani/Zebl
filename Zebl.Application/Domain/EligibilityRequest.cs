namespace Zebl.Application.Domain;

public class EligibilityRequest
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }

    public int PatientId { get; set; }

    public int PayerId { get; set; }

    public string SubscriberId { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public string? BatchFileName { get; set; }

    /// <summary>Physician NPI used on the 270 (for support/debug).</summary>
    public string? ProviderNpi { get; set; }

    /// <summary>Program Setup provider mode at request time (e.g. Billing, Rendering, Specific).</summary>
    public string? ProviderMode { get; set; }

    /// <summary>True when PayEligibilityPhyID was used as the provider source.</summary>
    public bool UsedPayerOverride { get; set; }
}

