using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Service for inserting Claim_Audit entries (EZClaim-style history). Implements IClaimAuditService for use from payment engine and elsewhere.
/// </summary>
public class ClaimAuditService : IClaimAuditService
{
    private readonly ZeblDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<ClaimAuditService> _logger;

    public ClaimAuditService(
        ZeblDbContext db,
        ICurrentUserContext userContext,
        ILogger<ClaimAuditService> logger)
    {
        _db = db;
        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordClaimEditedAsync(int claimId, string activityType, string notes, CancellationToken cancellationToken = default)
    {
        if (claimId <= 0) return;
        try
        {
            decimal totalCharge = 0;
            decimal insBalance = 0;
            decimal patBalance = 0;
            var snapshot = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claimId)
                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot != null)
            {
                totalCharge = snapshot.ClaTotalChargeTRIG;
                insBalance = snapshot.ClaTotalInsBalanceTRIG;
                patBalance = snapshot.ClaTotalPatBalanceTRIG;
            }

            _db.Claim_Audits.Add(new Claim_Audit
            {
                ClaFID = claimId,
                ActivityType = activityType ?? "Claim Edited",
                ActivityDate = DateTime.UtcNow,
                UserName = _userContext.UserName ?? "SYSTEM",
                ComputerName = _userContext.ComputerName ?? Environment.MachineName,
                Notes = notes ?? "Claim edited.",
                TotalCharge = totalCharge,
                InsuranceBalance = insBalance,
                PatientBalance = patBalance
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claim_Audit insert failed for claim {ClaId} ({ActivityType}).", claimId, activityType);
        }
    }

    /// <inheritdoc />
    public Task AddInsuranceEditedAsync(int claimId, CancellationToken cancellationToken = default)
        => RecordClaimEditedAsync(claimId, "Insurance Edited", "Claim insurance information edited.", cancellationToken);
}
