using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Service for inserting Claim_Audit entries (EZClaim-style history).
/// Call AddPaymentsAppliedAsync when payment posting is implemented.
/// </summary>
public class ClaimAuditService
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

    /// <summary>
    /// Inserts a "Payments applied." history row for the claim.
    /// Call this when payment posting applies payments to a claim.
    /// </summary>
    public async Task AddPaymentsAppliedAsync(int claId, CancellationToken cancellationToken = default)
    {
        if (claId <= 0) return;
        try
        {
            var snapshot = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot == null) return;

            _db.Claim_Audits.Add(new Claim_Audit
            {
                ClaFID = claId,
                ActivityType = "Payment Applied",
                ActivityDate = DateTime.UtcNow,
                UserName = _userContext.UserName ?? "SYSTEM",
                ComputerName = _userContext.ComputerName ?? Environment.MachineName,
                Notes = "Payments applied.",
                TotalCharge = snapshot.ClaTotalChargeTRIG,
                InsuranceBalance = snapshot.ClaTotalInsBalanceTRIG,
                PatientBalance = snapshot.ClaTotalPatBalanceTRIG
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claim_Audit insert failed for claim {ClaId} (Payments applied).", claId);
        }
    }

    /// <summary>
    /// Inserts a "Claim insurance information edited." history row for the claim.
    /// Call this when insurance (Claim_Insured) is updated.
    /// </summary>
    public async Task AddInsuranceEditedAsync(int claId, CancellationToken cancellationToken = default)
    {
        if (claId <= 0) return;
        try
        {
            var snapshot = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claId)
                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                .FirstOrDefaultAsync(cancellationToken);
            if (snapshot == null) return;

            _db.Claim_Audits.Add(new Claim_Audit
            {
                ClaFID = claId,
                ActivityType = "Insurance Edited",
                ActivityDate = DateTime.UtcNow,
                UserName = _userContext.UserName ?? "SYSTEM",
                ComputerName = _userContext.ComputerName ?? Environment.MachineName,
                Notes = "Claim insurance information edited.",
                TotalCharge = snapshot.ClaTotalChargeTRIG,
                InsuranceBalance = snapshot.ClaTotalInsBalanceTRIG,
                PatientBalance = snapshot.ClaTotalPatBalanceTRIG
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claim_Audit insert failed for claim {ClaId} (Insurance edited).", claId);
        }
    }
}
