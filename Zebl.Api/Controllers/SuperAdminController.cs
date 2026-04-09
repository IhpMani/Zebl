using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Api.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

/// <summary>SaaS onboarding: tenants, facilities, tenant admins. JWT must include <c>isSuperAdmin=true</c> (see policy <c>SuperAdminOnly</c>).</summary>
[ApiController]
[Route("api/super-admin")]
[Authorize(Policy = "SuperAdminOnly")]
public sealed class SuperAdminController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditTrail _auditTrail;

    public SuperAdminController(ZeblDbContext db, ICurrentUserContext currentUser, IAuditTrail auditTrail)
    {
        _db = db;
        _currentUser = currentUser;
        _auditTrail = auditTrail;
    }

    /// <summary>Active tenants for super-admin UI dropdowns. Returns <c>[]</c> when none match (never 405).</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.TenantId)
            .Select(t => new
            {
                t.TenantId,
                t.Name,
                t.TenantKey,
                CreatedDate = (DateTime?)null
            })
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    /// <summary>Facilities for one tenant (super-admin UI).</summary>
    [HttpGet("facilities")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetFacilities([FromQuery] int tenantId, CancellationToken cancellationToken) =>
        GetFacilitiesForTenantAsync(tenantId, cancellationToken);

    /// <summary>Same as GET facilities?tenantId= — alternate route for clients that had 405 on query style.</summary>
    [HttpGet("tenants/{tenantId:int}/facilities")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetFacilitiesByTenantRoute(int tenantId, CancellationToken cancellationToken) =>
        GetFacilitiesForTenantAsync(tenantId, cancellationToken);

    private async Task<IActionResult> GetFacilitiesForTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
            return BadRequest(new { error = "tenantId is required." });

        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.IsActive, cancellationToken);
        if (!tenantOk)
            return BadRequest(new { error = "Tenant does not exist or is inactive." });

        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        var rows = await _db.FacilityScopes.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.IsActive)
            .OrderBy(f => f.FacilityId)
            .Select(f => new
            {
                f.FacilityId,
                f.Name,
                f.TenantId,
                TenantName = tenantName,
                CreatedDate = (DateTime?)null
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Users for one tenant (super-admin UI).</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
            return BadRequest(new { error = "tenantId is required." });

        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.IsActive, cancellationToken);
        if (!tenantOk)
            return BadRequest(new { error = "Tenant does not exist or is inactive." });

        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        var rows = await _db.AppUsers.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.UserName)
            .Select(u => new
            {
                u.UserName,
                u.TenantId,
                TenantName = tenantName,
                Role = u.IsSuperAdmin ? "Super Admin" : "User",
                u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    /// <summary>Soft-deactivate a tenant, its active facilities, and inbound integrations for that tenant.</summary>
    [HttpDelete("tenants/{tenantId:int}")]
    public async Task<IActionResult> DeactivateTenant(int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
        if (tenant == null)
            return NotFound();

        if (!tenant.IsActive)
        {
            return Ok();
        }

        var facilities = await _db.FacilityScopes
            .Where(f => f.TenantId == tenantId && f.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var f in facilities)
            f.IsActive = false;

        var integrations = await _db.InboundIntegrations
            .Where(i => i.TenantId == tenantId && i.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var i in integrations)
            i.IsActive = false;

        tenant.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (string.IsNullOrWhiteSpace(request.TenantKey))
            return BadRequest(new { error = "TenantKey is required." });

        var tenantKey = request.TenantKey.Trim().ToLowerInvariant();
        var keyTaken = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantKey == tenantKey, cancellationToken);
        if (keyTaken)
            return Conflict(new { error = "TenantKey already exists." });

        var maxId = await _db.Tenants.AsNoTracking().Select(t => (int?)t.TenantId).MaxAsync(cancellationToken) ?? 0;
        var tenant = new Tenant
        {
            TenantId = maxId + 1,
            TenantKey = tenantKey,
            Name = request.Name.Trim(),
            IsActive = true
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditTrail.WriteAsync(
            _currentUser.UserId,
            tenant.TenantId,
            new AuditMetadata
            {
                Action = "TenantCreated",
                Actor = _currentUser.UserId?.ToString() ?? "",
                Target = tenant.TenantId.ToString(),
                Context = $"{tenant.Name}|{tenant.TenantKey}"
            },
            cancellationToken);

        return Ok(new { tenantId = tenant.TenantId });
    }

    [HttpPost("facilities")]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TenantId <= 0 || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "tenantId and name are required." });

        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantId == request.TenantId && t.IsActive, cancellationToken);
        if (!tenantOk)
            return BadRequest(new { error = "Tenant does not exist or is inactive." });

        var maxF = await _db.FacilityScopes.AsNoTracking().Select(f => (int?)f.FacilityId).MaxAsync(cancellationToken) ?? 0;
        var facility = new FacilityScope
        {
            FacilityId = maxF + 1,
            TenantId = request.TenantId,
            Name = request.Name.Trim(),
            IsActive = true
        };
        _db.FacilityScopes.Add(facility);

        var maxIntegId = await _db.InboundIntegrations.AsNoTracking().Select(i => (int?)i.Id).MaxAsync(cancellationToken) ?? 0;
        var integName = $"{facility.Name} HL7";
        if (integName.Length > 100)
            integName = integName[..100];
        _db.InboundIntegrations.Add(new InboundIntegration
        {
            Id = maxIntegId + 1,
            Name = integName,
            TenantId = request.TenantId,
            FacilityId = facility.FacilityId,
            IsActive = true
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _auditTrail.WriteAsync(
            _currentUser.UserId,
            request.TenantId,
            new AuditMetadata
            {
                Action = "FacilityCreated",
                Actor = _currentUser.UserId?.ToString() ?? "",
                Target = facility.FacilityId.ToString(),
                Context = $"{request.TenantId}|{facility.Name}"
            },
            cancellationToken);

        return Ok(new { facilityId = facility.FacilityId });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateTenantAdmin([FromBody] CreateTenantAdminRequest request, CancellationToken cancellationToken)
    {
        if (request == null ||
            request.TenantId <= 0 ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.FacilityId <= 0)
            return BadRequest(new { error = "tenantId, userName, password, and facilityId are required." });

        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantId == request.TenantId && t.IsActive, cancellationToken);
        if (!tenantOk)
            return BadRequest(new { error = "Tenant does not exist or is inactive." });

        var userName = request.UserName.Trim();
        var exists = await _db.AppUsers.AsNoTracking().AnyAsync(u => u.UserName == userName, cancellationToken);
        if (exists)
            return Conflict(new { error = "UserName already exists." });

        var user = PasswordHelper.CreateUser(userName, null, request.Password);
        user.TenantId = request.TenantId;
        user.IsSuperAdmin = false;

        await _db.AppUsers.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var fid = request.FacilityId;
        var facOk = await _db.FacilityScopes.AsNoTracking()
            .AnyAsync(
                f => f.FacilityId == fid && f.TenantId == request.TenantId && f.IsActive,
                cancellationToken);
        if (!facOk)
            return BadRequest(new { error = "facilityId is not an active facility for this tenant." });

        _db.UserFacilities.Add(new UserFacility { UserId = user.UserGuid, FacilityId = fid });
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { userGuid = user.UserGuid, userName = user.UserName, tenantId = user.TenantId });
    }
}

public sealed class CreateTenantRequest
{
    /// <summary>Stable key for <c>X-Tenant-Key</c> (stored lower-case).</summary>
    public string TenantKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class CreateFacilityRequest
{
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateTenantAdminRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TenantId { get; set; }

    /// <summary>Required: single facility the user may access.</summary>
    public int FacilityId { get; set; }
}
