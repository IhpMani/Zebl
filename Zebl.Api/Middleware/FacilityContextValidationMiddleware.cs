using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Middleware;

public sealed class FacilityContextValidationMiddleware
{
    private const string FacilityHeader = "X-Facility-Id";
    private const string TenantKeyHeader = "X-Tenant-Key";

    private readonly RequestDelegate _next;

    public FacilityContextValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ZeblDbContext db,
        ICurrentUserContext userContext,
        ITenantContext tenantContext,
        IAdminUserService adminUserService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        /* super-admin routes: no tenant header enforcement; facility not required */
        if (path.StartsWith("/api/super-admin", StringComparison.OrdinalIgnoreCase))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var jwtSuper = JwtValidatedTenantContext.IsJwtSuperAdmin(context.User);
            if (jwtSuper)
            {
                await _next(context);
                return;
            }

            var uid = userContext.UserId;
            if (uid is not null && uid.Value != JwtCurrentUserContext.SystemUserId)
            {
                var isSuperAdmin = await db.AppUsers.AsNoTracking()
                    .AnyAsync(u => u.UserGuid == uid.Value && u.IsSuperAdmin, context.RequestAborted);
                if (isSuperAdmin)
                {
                    await _next(context);
                    return;
                }
            }

            await WriteError(context, StatusCodes.Status403Forbidden, "SUPER_ADMIN_REQUIRED",
                "Super admin access is required for this endpoint.");
            return;
        }

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (OperationalContextWithoutFacility(path, context.Request.Method))
        {
            if (HttpMethods.IsGet(context.Request.Method) &&
                path.StartsWith("/api/facilities", StringComparison.OrdinalIgnoreCase) &&
                JwtValidatedTenantContext.IsJwtSuperAdmin(context.User))
            {
                await WriteError(
                    context,
                    StatusCodes.Status403Forbidden,
                    "IMPERSONATION_REQUIRED",
                    "Platform administrators must call POST /api/super-admin/impersonate before using tenant-facing endpoints.");
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true &&
                !JwtValidatedTenantContext.IsJwtSuperAdmin(context.User) &&
                !await TenantHeaderMatchesJwtAsync(context, db, context.RequestAborted))
            {
                await WriteError(
                    context,
                    StatusCodes.Status403Forbidden,
                    "TENANT_MISMATCH",
                    "X-Tenant-Key does not match the signed-in user's tenant.");
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true &&
                !JwtValidatedTenantContext.IsJwtSuperAdmin(context.User))
            {
                var opUid = userContext.UserId;
                if (opUid is not null && opUid.Value != JwtCurrentUserContext.SystemUserId)
                {
                    var opSa = await db.AppUsers.AsNoTracking()
                        .AnyAsync(u => u.UserGuid == opUid.Value && u.IsSuperAdmin, context.RequestAborted);
                    var opImpersonation = string.Equals(
                        context.User.FindFirst("impersonation")?.Value,
                        "true",
                        StringComparison.OrdinalIgnoreCase);
                    if (!opSa && !opImpersonation)
                    {
                        var hasMapping = await db.UserFacilities.AsNoTracking()
                            .AnyAsync(uf => uf.UserId == opUid.Value, context.RequestAborted);
                        if (!hasMapping)
                        {
                            await WriteError(
                                context,
                                StatusCodes.Status403Forbidden,
                                "NO_FACILITY_ACCESS",
                                "No explicit facility assignment exists for this user.");
                            return;
                        }
                    }
                }
            }

            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(FacilityHeader, out var headerValues))
        {
            await WriteError(context, StatusCodes.Status400BadRequest, "INVALID_FACILITY",
                "X-Facility-Id header is required.");
            return;
        }

        if (!int.TryParse(headerValues.ToString(), out var facilityId) || facilityId <= 0)
        {
            await WriteError(context, StatusCodes.Status400BadRequest, "INVALID_FACILITY",
                "X-Facility-Id must be a positive integer.");
            return;
        }

        var scope = await db.FacilityScopes.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FacilityId == facilityId && f.IsActive, context.RequestAborted);
        if (scope == null)
        {
            await WriteError(context, StatusCodes.Status400BadRequest, "INVALID_FACILITY",
                "X-Facility-Id does not map to an active facility.");
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true &&
            !JwtValidatedTenantContext.IsJwtSuperAdmin(context.User) &&
            !await TenantHeaderMatchesJwtAsync(context, db, context.RequestAborted))
        {
            await WriteError(
                context,
                StatusCodes.Status403Forbidden,
                "TENANT_MISMATCH",
                "X-Tenant-Key does not match the signed-in user's tenant.");
            return;
        }

        if (scope.TenantId != tenantContext.TenantId)
        {
            await WriteError(context, StatusCodes.Status400BadRequest, "TENANT_FACILITY_MISMATCH",
                "X-Facility-Id is not in scope for the resolved tenant.");
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await WriteError(context, StatusCodes.Status403Forbidden, "FACILITY_ACCESS_DENIED",
                "User does not have access to this facility.");
            return;
        }

        if (adminUserService.IsAdminUser(userContext.UserName))
        {
            await _next(context);
            return;
        }

        var userId = userContext.UserId;
        if (userId is null || userId.Value == JwtCurrentUserContext.SystemUserId)
        {
            await WriteError(context, StatusCodes.Status403Forbidden, "FACILITY_ACCESS_DENIED",
                "User does not have access to this facility.");
            return;
        }

        var isPlatformSuperAdmin = await db.AppUsers.AsNoTracking()
            .AnyAsync(u => u.UserGuid == userId.Value && u.IsSuperAdmin, context.RequestAborted);
        if (isPlatformSuperAdmin)
        {
            await _next(context);
            return;
        }

        var hasAnyUserFacility = await db.UserFacilities.AsNoTracking()
            .AnyAsync(uf => uf.UserId == userId.Value, context.RequestAborted);
        if (!hasAnyUserFacility)
        {
            await WriteError(
                context,
                StatusCodes.Status403Forbidden,
                "NO_FACILITY_ACCESS",
                "No explicit facility assignment exists for this user.");
            return;
        }

        var allowed = await db.UserFacilities.AsNoTracking()
            .AnyAsync(uf => uf.UserId == userId.Value && uf.FacilityId == facilityId, context.RequestAborted);
        if (!allowed)
        {
            if (!await ImpersonationAllowsFacilityAsync(
                    context, db, userId.Value, facilityId, scope.TenantId, context.RequestAborted))
            {
                await WriteError(context, StatusCodes.Status403Forbidden, "FACILITY_ACCESS_DENIED",
                    "User does not have access to this facility.");
                return;
            }
        }

        await _next(context);
    }

    private static bool OperationalContextWithoutFacility(string path, string method) =>
        HttpMethods.IsGet(method) &&
        (path.StartsWith("/api/facilities", StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith("/api/integrations/by-facility", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Super-admin "enter tenant" JWT (<c>impersonation</c>) is not in UserFacilities; allow facilities in the JWT tenant.
    /// </summary>
    private static async Task<bool> ImpersonationAllowsFacilityAsync(
        HttpContext context,
        ZeblDbContext db,
        Guid userId,
        int facilityId,
        int scopeTenantId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(context.User.FindFirst("impersonation")?.Value, "true",
                StringComparison.OrdinalIgnoreCase))
            return false;

        var isSa = await db.AppUsers.AsNoTracking()
            .AnyAsync(u => u.UserGuid == userId && u.IsSuperAdmin, cancellationToken);
        if (!isSa)
            return false;

        if (!int.TryParse(context.User.FindFirst("tenantId")?.Value, out var jwtTid) ||
            jwtTid <= 0 ||
            jwtTid != scopeTenantId)
            return false;

        if (int.TryParse(context.User.FindFirst("facilityId")?.Value, out var jwtFid) && jwtFid > 0)
            return jwtFid == facilityId;

        return true;
    }

    private static async Task<bool> TenantHeaderMatchesJwtAsync(
        HttpContext context,
        ZeblDbContext db,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(context.User.FindFirst("tenantId")?.Value, out var jwtTenantId) ||
            jwtTenantId <= 0)
            return false;

        if (!context.Request.Headers.TryGetValue(TenantKeyHeader, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
            return false;

        var headerKey = raw.ToString().Trim().ToLowerInvariant();
        var claimKey = context.User.FindFirst("tenantKey")?.Value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(claimKey))
        {
            claimKey = await db.Tenants.AsNoTracking()
                .Where(t => t.TenantId == jwtTenantId && t.IsActive)
                .Select(t => t.TenantKey)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return !string.IsNullOrEmpty(claimKey) && headerKey == claimKey;
    }

    private static Task WriteError(HttpContext context, int statusCode, string errorCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ErrorResponseDto
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = context.TraceIdentifier
        });
    }
}
