namespace Zebl.Application.Dtos.Payers;

public class CreatePayerRequest
{
    public string? PayName { get; set; }
    public string? PayExternalID { get; set; }
    public string? PayAddr1 { get; set; }
    public string? PayAddr2 { get; set; }
    public string? PayBox1 { get; set; }
    public string? PayCity { get; set; }
    public string? PayState { get; set; }
    public string? PayZip { get; set; }
    public string? PayPhoneNo { get; set; }
    public string? PayEmail { get; set; }
    public string? PayFaxNo { get; set; }
    public string? PayWebsite { get; set; }
    public string? PayNotes { get; set; }
    public string? PayOfficeNumber { get; set; }
    public string PaySubmissionMethod { get; set; } = "Paper";
    public string? PayClaimFilingIndicator { get; set; }
    public string PayClaimType { get; set; } = "Professional";
    public string? PayInsTypeCode { get; set; }
    public string? PayClassification { get; set; }
    public string? PayPaymentMatchingKey { get; set; }
    public string? PayEligibilityPayerID { get; set; }
    public int PayEligibilityPhyID { get; set; }
    public int PayFollowUpDays { get; set; }
    public string? PayICDIndicator { get; set; }
    public bool PayInactive { get; set; }
    public bool PayIgnoreRenderingProvider { get; set; }
    public bool PayForwardsClaims { get; set; }
    public bool PayExportAuthIn2400 { get; set; }
    public bool PayExportSSN { get; set; }
    public bool PayExportOriginalRefIn2330B { get; set; }
    public bool PayExportPaymentDateIn2330B { get; set; }
    public bool PayExportPatientAmtDueIn2430 { get; set; }
    public bool PayUseTotalAppliedInBox29 { get; set; }
    public bool PayPrintBox30 { get; set; }
    public bool PaySuppressWhenPrinting { get; set; }
}
