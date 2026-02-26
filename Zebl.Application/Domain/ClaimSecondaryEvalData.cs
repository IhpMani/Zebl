namespace Zebl.Application.Domain;

/// <summary>
/// Claim data needed for secondary trigger evaluation and for copying when creating secondary claim.
/// </summary>
public class ClaimSecondaryEvalData
{
    public int ClaimId { get; set; }
    public int PatientId { get; set; }
    public string? Status { get; set; }
    public decimal TotalBalance { get; set; }
    public int PrimaryPayerId { get; set; }
    public int? SecondaryPayerId { get; set; }
    public int BillingPhysicianId { get; set; }
    public int RenderingPhysicianId { get; set; }
    public int AttendingPhysicianId { get; set; }
    public int FacilityPhysicianId { get; set; }
    public int ReferringPhysicianId { get; set; }
    public int SupervisingPhysicianId { get; set; }
    public int OperatingPhysicianId { get; set; }
    public int OrderingPhysicianId { get; set; }
    public DateOnly? BillDate { get; set; }
    public string? Diagnosis1 { get; set; }
    public string? Diagnosis2 { get; set; }
    public string? Diagnosis3 { get; set; }
    public string? Diagnosis4 { get; set; }
    public string? Diagnosis5 { get; set; }
    public string? ICDIndicator { get; set; }
    public string? SubmissionMethod { get; set; }
    public string? ClaimType { get; set; }
    public int? PrimaryClaimFID { get; set; }
}
