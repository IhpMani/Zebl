namespace Zebl.Infrastructure.Persistence.Entities;

public class SendingClaimsSettings : ITenantFacilityEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public bool ShowBillToPatientClaims { get; set; }
    public string PatientControlNumberMode { get; set; } = "ClaimId";
    public int NextSubmissionNumber { get; set; } = 1;
}
