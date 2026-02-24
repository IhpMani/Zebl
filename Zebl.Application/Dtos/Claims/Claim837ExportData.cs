using Zebl.Application.Domain;

namespace Zebl.Application.Dtos.Claims;

/// <summary>
/// All data required to generate an 837 for one claim. Application-layer DTO; populated by Infrastructure.
/// </summary>
public class Claim837ExportData
{
    public int ClaimId { get; set; }
    public string? ClaInsuranceTypeCodeOverride { get; set; }
    public DateOnly? ClaBillDate { get; set; }
    public DateOnly? ClaStatementCoversFromOverride { get; set; }
    public DateOnly? ClaStatementCoversThroughOverride { get; set; }
    public string? PlaceOfService { get; set; }
    public string? ClaDiagnosis1 { get; set; }
    public string? ClaDiagnosis2 { get; set; }
    public string? ClaReferralNumber { get; set; }
    public string? ClaPriorAuthorizationNumber { get; set; }

    /// <summary>Primary insured (ClaInsSequence = 1).</summary>
    public ClaimInsuredExportDto? PrimaryInsured { get; set; }

    /// <summary>Patient (from Claim.ClaPatFID).</summary>
    public PatientExportDto? Patient { get; set; }

    /// <summary>Payer for primary insured. Required for electronic submission.</summary>
    public Payer? Payer { get; set; }

    /// <summary>Billing provider (ClaBillingPhyFID).</summary>
    public ProviderExportDto? BillingProvider { get; set; }

    /// <summary>Rendering provider (ClaRenderingPhyFID). Omitted if PayIgnoreRenderingProvider.</summary>
    public ProviderExportDto? RenderingProvider { get; set; }
}

public class ClaimInsuredExportDto
{
    public string? ClaInsClaimFilingIndicator { get; set; }
    public string? ClaInsSSN { get; set; }
    public string? ClaInsFirstName { get; set; }
    public string? ClaInsLastName { get; set; }
    public string? ClaInsIDNumber { get; set; }
    public string? ClaInsGroupNumber { get; set; }
    public DateOnly? ClaInsBirthDate { get; set; }
    public string? ClaInsAddress { get; set; }
    public string? ClaInsCity { get; set; }
    public string? ClaInsState { get; set; }
    public string? ClaInsZip { get; set; }
    public string? ClaInsSex { get; set; }
    public int? ClaInsSequence { get; set; }
}

public class PatientExportDto
{
    public string? PatFirstName { get; set; }
    public string? PatLastName { get; set; }
    public string? PatMI { get; set; }
    public DateOnly? PatBirthDate { get; set; }
    public string? PatAddress { get; set; }
    public string? PatCity { get; set; }
    public string? PatState { get; set; }
    public string? PatZip { get; set; }
    public string? PatSex { get; set; }
}

public class ProviderExportDto
{
    public string? PhyNPI { get; set; }
    public string? PhyName { get; set; }
    public string? PhyFirstName { get; set; }
    public string? PhyLastName { get; set; }
    public string? PhyAddress1 { get; set; }
    public string? PhyCity { get; set; }
    public string? PhyState { get; set; }
    public string? PhyZip { get; set; }
}
