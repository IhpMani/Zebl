namespace Zebl.Application.Services;

public sealed class SendingClaimsSettingsDto
{
    public bool ShowBillToPatientClaims { get; set; }
    public string PatientControlNumberMode { get; set; } = "ClaimId";
    public int NextSubmissionNumber { get; set; } = 1;
}

public interface ISendingClaimsSettingsService
{
    Task<SendingClaimsSettingsDto> GetSettingsAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default);
    Task<SendingClaimsSettingsDto> UpdateSettingsAsync(int tenantId, int facilityId, SendingClaimsSettingsDto settings, CancellationToken cancellationToken = default);
    Task<int> GetAndIncrementSubmissionNumberAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default);
    Task<string> GetPatientControlNumberModeAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default);
}
