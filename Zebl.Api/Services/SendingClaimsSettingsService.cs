using Microsoft.EntityFrameworkCore;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

public class SendingClaimsSettingsService : ISendingClaimsSettingsService
{
    private readonly ZeblDbContext _db;

    public SendingClaimsSettingsService(ZeblDbContext db)
    {
        _db = db;
    }

    public async Task<SendingClaimsSettingsDto> GetSettingsAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(tenantId, facilityId, cancellationToken);
        return ToDto(entity);
    }

    public async Task<SendingClaimsSettingsDto> UpdateSettingsAsync(int tenantId, int facilityId, SendingClaimsSettingsDto settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);

        var entity = await EnsureEntityAsync(tenantId, facilityId, cancellationToken);
        entity.ShowBillToPatientClaims = settings.ShowBillToPatientClaims;
        entity.PatientControlNumberMode = NormalizeMode(settings.PatientControlNumberMode);
        entity.NextSubmissionNumber = settings.NextSubmissionNumber;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<int> GetAndIncrementSubmissionNumberAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(tenantId, facilityId, cancellationToken);
        var current = entity.NextSubmissionNumber < 1 ? 1 : entity.NextSubmissionNumber;
        entity.NextSubmissionNumber = current + 1;
        await _db.SaveChangesAsync(cancellationToken);
        return current;
    }

    public async Task<string> GetPatientControlNumberModeAsync(int tenantId, int facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(tenantId, facilityId, cancellationToken);
        return NormalizeMode(entity.PatientControlNumberMode);
    }

    private static void Validate(SendingClaimsSettingsDto settings)
    {
        if (settings.NextSubmissionNumber < 1)
            throw new ArgumentException("NextSubmissionNumber must be >= 1.");
        settings.PatientControlNumberMode = NormalizeMode(settings.PatientControlNumberMode);
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, "PatientAccount", StringComparison.OrdinalIgnoreCase))
            return "PatientAccount";
        return "ClaimId";
    }

    private async Task<SendingClaimsSettings> EnsureEntityAsync(int tenantId, int facilityId, CancellationToken cancellationToken)
    {
        var entity = await _db.SendingClaimsSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.FacilityId == facilityId, cancellationToken);
        if (entity != null)
            return entity;

        entity = new SendingClaimsSettings
        {
            TenantId = tenantId,
            FacilityId = facilityId,
            ShowBillToPatientClaims = false,
            PatientControlNumberMode = "ClaimId",
            NextSubmissionNumber = 1
        };
        _db.SendingClaimsSettings.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static SendingClaimsSettingsDto ToDto(SendingClaimsSettings entity)
    {
        return new SendingClaimsSettingsDto
        {
            ShowBillToPatientClaims = entity.ShowBillToPatientClaims,
            PatientControlNumberMode = NormalizeMode(entity.PatientControlNumberMode),
            NextSubmissionNumber = entity.NextSubmissionNumber < 1 ? 1 : entity.NextSubmissionNumber
        };
    }
}
