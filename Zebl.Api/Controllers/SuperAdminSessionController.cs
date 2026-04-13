using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

/// <summary>Super-admin session: impersonate tenant (operational JWT) or exit back to platform token.</summary>
[ApiController]
[Route("api/super-admin")]
public sealed class SuperAdminSessionController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly IJwtTokenIssuer _jwtIssuer;
    private readonly ICurrentUserContext _userContext;
    private readonly IAdminUserService _adminUserService;
    private readonly IAuditTrail _auditTrail;

    public SuperAdminSessionController(
        ZeblDbContext db,
        IJwtTokenIssuer jwtIssuer,
        ICurrentUserContext userContext,
        IAdminUserService adminUserService,
        IAuditTrail auditTrail)
    {
        _db = db;
        _jwtIssuer = jwtIssuer;
        _userContext = userContext;
        _adminUserService = adminUserService;
        _auditTrail = auditTrail;
    }

    /// <summary>Issue an operational JWT for the same super-admin user (tenant/facility scoped). Requires current JWT with isSuperAdmin=true.</summary>
    [HttpPost("impersonate")]
    [Authorize(Policy = "SuperAdminOnly")]
    [EnableRateLimiting("auth-security")]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TenantId <= 0)
            return BadRequest(new { error = "tenantId is required." });

        var uid = _userContext.UserId;
        if (uid is null || uid.Value == JwtCurrentUserContext.SystemUserId)
            return Unauthorized();

        var appUser = await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserGuid == uid.Value && u.IsActive, cancellationToken);
        if (appUser == null || !appUser.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Super admin only." });

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId && t.IsActive, cancellationToken);
        if (tenant == null)
            return BadRequest(new { error = "Tenant does not exist or is inactive." });

        int? facilityId = request.FacilityId;
        if (facilityId is int fid && fid > 0)
        {
            var ok = await _db.FacilityScopes.AsNoTracking()
                .AnyAsync(f => f.FacilityId == fid && f.TenantId == request.TenantId && f.IsActive, cancellationToken);
            if (!ok)
                return BadRequest(new { error = "Facility does not belong to this tenant or is inactive." });
        }

        var isAdmin = _adminUserService.IsAdminUser(appUser.UserName);
        var sessionStamp = appUser.SessionStamp ?? string.Empty;
        var token = _jwtIssuer.IssueOperationalToken(
            appUser.UserGuid,
            appUser.UserName,
            isAdmin,
            tenant.TenantId,
            facilityId,
            tenant.TenantKey,
            impersonation: true,
            sessionStamp);

        await _auditTrail.WriteAsync(
            uid,
            request.TenantId,
            new AuditMetadata
            {
                Action = "SuperAdminImpersonate",
                Actor = uid.Value.ToString(),
                Target = request.TenantId.ToString(),
                Context = facilityId?.ToString() ?? ""
            },
            cancellationToken);

        return Ok(new ImpersonateResponse
        {
            Token = token,
            ExpiresAtUtc = _jwtIssuer.GetUtcExpiry(),
            IsSuperAdmin = false,
            TenantId = tenant.TenantId,
            FacilityId = facilityId,
            TenantKey = tenant.TenantKey
        });
    }

    /// <summary>Restore platform super-admin JWT for users who are super admins in the database.</summary>
    [HttpPost("exit")]
    [Authorize(Policy = "RequireAuth")]
    public async Task<IActionResult> ExitImpersonation(CancellationToken cancellationToken)
    {
        var uid = _userContext.UserId;
        if (uid is null || uid.Value == JwtCurrentUserContext.SystemUserId)
            return Unauthorized();

        var appUser = await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserGuid == uid.Value && u.IsActive, cancellationToken);
        if (appUser == null || !appUser.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only platform super admins may exit to a super-admin session." });

        var isAdmin = _adminUserService.IsAdminUser(appUser.UserName);
        var sessionStamp = appUser.SessionStamp ?? string.Empty;
        var token = _jwtIssuer.IssueSuperAdminToken(appUser.UserGuid, appUser.UserName, isAdmin, sessionStamp);

        await _auditTrail.WriteAsync(
            uid,
            null,
            new AuditMetadata
            {
                Action = "SuperAdminExitImpersonation",
                Actor = uid.Value.ToString(),
                Target = "",
                Context = ""
            },
            cancellationToken);

        return Ok(new ImpersonateResponse
        {
            Token = token,
            ExpiresAtUtc = _jwtIssuer.GetUtcExpiry(),
            IsSuperAdmin = true,
            TenantId = null,
            FacilityId = null,
            TenantKey = null
        });
    }
}

public sealed class ImpersonateRequest
{
    public int TenantId { get; set; }
    public int? FacilityId { get; set; }
}

public sealed class ImpersonateResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int? TenantId { get; set; }
    public int? FacilityId { get; set; }
    public string? TenantKey { get; set; }
}
