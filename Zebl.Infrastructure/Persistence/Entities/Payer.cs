using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Payer
{
    public int PayID { get; set; }

    public DateTime PayDateTimeCreated { get; set; }

    public DateTime PayDateTimeModified { get; set; }

    public Guid? PayCreatedUserGUID { get; set; }

    public Guid? PayLastUserGUID { get; set; }

    public string? PayCreatedUserName { get; set; }

    public string? PayLastUserName { get; set; }

    public string? PayCreatedComputerName { get; set; }

    public string? PayLastComputerName { get; set; }

    public string? PayName { get; set; }

    public string? PayExternalID { get; set; }

    public string? PayAddr1 { get; set; }

    public string? PayAddr2 { get; set; }

    public bool PayAlwaysExportSupervisingProvider { get; set; }

    public string? PayBox1 { get; set; }

    public string? PayCity { get; set; }

    public string? PayClaimFilingIndicator { get; set; }

    public string PayClaimType { get; set; } = null!;

    public string? PayClassification { get; set; }

    public int PayEligibilityPhyID { get; set; }

    public string? PayEligibilityPayerID { get; set; }

    public string? PayEmail { get; set; }

    public bool PayExportAuthIn2400 { get; set; }

    public bool PayExportBillingTaxonomy { get; set; }

    public bool PayExportOtherPayerOfficeNumber2330B { get; set; }

    public bool PayExportOriginalRefIn2330B { get; set; }

    public bool PayExportPatientAmtDueIn2430 { get; set; }

    public bool PayExportPatientForPOS12 { get; set; }

    public bool PayExportPaymentDateIn2330B { get; set; }

    public bool PayExportSSN { get; set; }

    public string? PayFaxNo { get; set; }

    public int PayFollowUpDays { get; set; }

    public bool PayForwardsClaims { get; set; }

    public string? PayICDIndicator { get; set; }

    public bool PayIgnoreRenderingProvider { get; set; }

    public bool PayInactive { get; set; }

    public string? PayInsTypeCode { get; set; }

    public string? PayNotes { get; set; }

    public string? PayOfficeNumber { get; set; }

    public string? PayPaymentMatchingKey { get; set; }

    public string? PayPhoneNo { get; set; }

    public bool PayPrintBox30 { get; set; }

    public bool PayFormatDateBox14And15 { get; set; }

    public string? PayState { get; set; }

    public string PaySubmissionMethod { get; set; } = null!;

    public bool PaySuppressWhenPrinting { get; set; }

    public decimal PayTotalUndisbursedPaymentsTRIG { get; set; }

    public bool PayExportTrackedPRAdjs { get; set; }

    public bool PayUseTotalAppliedInBox29 { get; set; }

    public string? PayWebsite { get; set; }

    public string? PayZip { get; set; }

    public string PayNameWithInactiveCC { get; set; } = null!;

    public string PayCityStateZipCC { get; set; } = null!;

    public virtual ICollection<Adjustment> Adjustments { get; set; } = new List<Adjustment>();

    public virtual ICollection<Claim_Insured> Claim_Insureds { get; set; } = new List<Claim_Insured>();

    public virtual ICollection<Insured> Insureds { get; set; } = new List<Insured>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Procedure_Code> Procedure_Codes { get; set; } = new List<Procedure_Code>();

    public virtual ICollection<Service_Line> Service_Lines { get; set; } = new List<Service_Line>();
}
