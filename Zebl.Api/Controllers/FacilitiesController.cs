using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers;

/// <summary>Operational facilities for the current user within the tenant from <c>X-Tenant-Key</c>.</summary>
[ApiController]
[Route("api/facilities")]
[Authorize(Policy = "RequireAuth")]
public sealed class FacilitiesController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAdminUserService _adminUserService;

    public FacilitiesController(
        ZeblDbContext db,
        ICurrentUserContext userContext,
        ITenantContext tenantContext,
        IAdminUserService adminUserService)
    {
        _db = db;
        _userContext = userContext;
        _tenantContext = tenantContext;
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetForCurrentContext(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var userId = _userContext.UserId;
        if (userId is null || userId.Value == JwtCurrentUserContext.SystemUserId)
            return Unauthorized();

        if (_adminUserService.IsAdminUser(_userContext.UserName))
        {
            var all = await _db.FacilityScopes.AsNoTracking()
                .Where(f => f.TenantId == tenantId && f.IsActive)
                .OrderBy(f => f.FacilityId)
                .Select(f => new { facilityId = f.FacilityId, name = f.Name, tenantId = f.TenantId })
                .ToListAsync(cancellationToken);
            return Ok(all);
        }

        var isSuper = await _db.AppUsers.AsNoTracking()
            .AnyAsync(u => u.UserGuid == userId.Value && u.IsSuperAdmin, cancellationToken);
        if (isSuper)
        {
            var all = await _db.FacilityScopes.AsNoTracking()
                .Where(f => f.TenantId == tenantId && f.IsActive)
                .OrderBy(f => f.FacilityId)
                .Select(f => new { facilityId = f.FacilityId, name = f.Name, tenantId = f.TenantId })
                .ToListAsync(cancellationToken);
            return Ok(all);
        }

        var mappedIds = await _db.UserFacilities.AsNoTracking()
            .Where(uf => uf.UserId == userId.Value)
            .Select(uf => uf.FacilityId)
            .ToListAsync(cancellationToken);

        if (mappedIds.Count == 0)
        {
            var appFac = await _db.AppUsers.AsNoTracking()
                .Where(u => u.UserGuid == userId.Value)
                .Select(u => u.FacilityId)
                .FirstOrDefaultAsync(cancellationToken);
            if (appFac is int fid && fid > 0)
                mappedIds.Add(fid);
        }

        var rows = await _db.FacilityScopes.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.IsActive && mappedIds.Contains(f.FacilityId))
            .OrderBy(f => f.FacilityId)
            .Select(f => new { facilityId = f.FacilityId, name = f.Name, tenantId = f.TenantId })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}
