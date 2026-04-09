using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize(Policy = "RequireAuth")]
public sealed class IntegrationsController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAdminUserService _adminUserService;

    public IntegrationsController(
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

    /// <summary>Active inbound integration id for HL7 (DFT) for the given facility.</summary>
    [HttpGet("by-facility")]
    public async Task<IActionResult> GetInboundIdByFacility([FromQuery] int facilityId, CancellationToken cancellationToken)
    {
        if (facilityId <= 0)
            return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "facilityId must be a positive integer." });

        var userId = _userContext.UserId;
        if (userId is null || userId.Value == JwtCurrentUserContext.SystemUserId)
            return Unauthorized();

        var tenantId = _tenantContext.TenantId;

        var scope = await _db.FacilityScopes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FacilityId == facilityId && f.TenantId == tenantId && f.IsActive, cancellationToken);
        if (scope == null)
            return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_FACILITY", Message = "Facility is not active for this tenant." });

        if (!await UserCanAccessFacilityAsync(userId.Value, facilityId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto { ErrorCode = "FACILITY_ACCESS_DENIED", Message = "User does not have access to this facility." });

        var existing = await _db.InboundIntegrations
            .FirstOrDefaultAsync(i => i.FacilityId == facilityId && i.TenantId == tenantId && i.IsActive, cancellationToken);

        if (existing != null)
            return Ok(new { integrationId = existing.Id });

        /* No row (e.g. after removal of model seed data): create a default HL7 inbound slot for this facility. */
        var maxId = await _db.InboundIntegrations.Select(i => (int?)i.Id).MaxAsync(cancellationToken) ?? 0;
        var label = $"{scope.Name} HL7";
        if (label.Length > 100)
            label = label[..100];

        var created = new InboundIntegration
        {
            Id = maxId + 1,
            Name = label,
            TenantId = tenantId,
            FacilityId = facilityId,
            IsActive = true
        };
        _db.InboundIntegrations.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { integrationId = created.Id });
    }

    private async Task<bool> UserCanAccessFacilityAsync(Guid userId, int facilityId, CancellationToken cancellationToken)
    {
        if (_adminUserService.IsAdminUser(_userContext.UserName))
            return true;

        if (await _db.AppUsers.AsNoTracking()
                .AnyAsync(u => u.UserGuid == userId && u.IsSuperAdmin, cancellationToken))
            return true;

        if (await _db.UserFacilities.AsNoTracking()
                .AnyAsync(uf => uf.UserId == userId && uf.FacilityId == facilityId, cancellationToken))
            return true;

        var appFac = await _db.AppUsers.AsNoTracking()
            .Where(u => u.UserGuid == userId)
            .Select(u => u.FacilityId)
            .FirstOrDefaultAsync(cancellationToken);

        return appFac == facilityId;
    }
}
