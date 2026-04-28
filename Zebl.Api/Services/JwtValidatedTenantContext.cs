using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Services;

/// <summary>
/// Tenant id comes from JWT (<c>tenantId</c>) for normal users; <c>X-Tenant-Key</c> is optional and only validated if supplied.
/// Platform super-admins resolve tenant from <c>X-Facility-Id</c> or <c>facilityId</c> query (not from header key alone).
/// </summary>
public sealed class JwtValidatedTenantContext : ITenantContext
{
    private const string TenantKeyHeader = "X-Tenant-Key";
    private const string FacilityHeader = "X-Facility-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbContextFactory<ZeblDbContext> _dbFactory;
    private int? _resolvedTenantId;

    public JwtValidatedTenantContext(IHttpContextAccessor httpContextAccessor, IDbContextFactory<ZeblDbContext> dbFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbFactory = dbFactory;
    }

    public int TenantId => ResolveTenantId();

    private int ResolveTenantId()
    {
        if (_resolvedTenantId.HasValue)
            return _resolvedTenantId.Value;

        var http = _httpContextAccessor.HttpContext
                   ?? throw new InvalidOperationException("HTTP context is unavailable for tenant resolution.");

        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("Authenticated user required for tenant resolution.");

        if (IsJwtSuperAdmin(user))
        {
            var path = http.Request.Path.Value ?? string.Empty;
            if (HttpMethods.IsGet(http.Request.Method) &&
                path.StartsWith("/api/facilities", StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantSecurityException(
                    "IMPERSONATION_REQUIRED",
                    "Use POST /api/super-admin/impersonate to enter a tenant before calling this endpoint.");
            }

            var facilityId = TryGetFacilityIdForSuperAdmin(http);
            if (facilityId is not int fid || fid <= 0)
            {
                throw new TenantSecurityException(
                    "FACILITY_REQUIRED",
                    "X-Facility-Id or facilityId query is required for platform operations.");
            }

            using var db = _dbFactory.CreateDbContext();
            var tenantFromFacility = db.FacilityScopes.AsNoTracking()
                .Where(f => f.FacilityId == fid && f.IsActive)
                .Select(f => f.TenantId)
                .FirstOrDefault();

            if (tenantFromFacility <= 0)
                throw new TenantSecurityException("INVALID_FACILITY", "Facility is not active.");

            _resolvedTenantId = tenantFromFacility;
            return _resolvedTenantId.Value;
        }

        if (!int.TryParse(user.FindFirst("tenantId")?.Value, out var jwtTenantId) || jwtTenantId <= 0)
            throw new TenantSecurityException("TENANT_REQUIRED", "JWT must include a valid tenantId claim.");

        if (http.Request.Headers.TryGetValue(TenantKeyHeader, out var rawHeader) &&
            !string.IsNullOrWhiteSpace(rawHeader))
        {
            var headerKey = rawHeader.ToString().Trim().ToLowerInvariant();
            var claimKey = user.FindFirst("tenantKey")?.Value?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(claimKey))
            {
                using var db = _dbFactory.CreateDbContext();
                claimKey = db.Tenants.AsNoTracking()
                    .Where(t => t.TenantId == jwtTenantId && t.IsActive)
                    .Select(t => t.TenantKey)
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(claimKey) || headerKey != claimKey)
            {
                throw new TenantSecurityException(
                    "TENANT_MISMATCH",
                    "X-Tenant-Key does not match the signed-in user's tenant. Header spoofing is not allowed.");
            }
        }

        _resolvedTenantId = jwtTenantId;
        return _resolvedTenantId.Value;
    }

    private static int? TryGetFacilityIdForSuperAdmin(HttpContext http)
    {
        if (http.Request.Headers.TryGetValue(FacilityHeader, out var hv) &&
            int.TryParse(hv.ToString(), out var headerFid) &&
            headerFid > 0)
            return headerFid;

        if (http.Request.Query.TryGetValue("facilityId", out Microsoft.Extensions.Primitives.StringValues qv) &&
            int.TryParse(qv.ToString(), out var qfid) &&
            qfid > 0)
            return qfid;

        return null;
    }

    internal static bool IsJwtSuperAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

}
