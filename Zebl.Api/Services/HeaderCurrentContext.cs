using Microsoft.AspNetCore.Http;
using Zebl.Application.Abstractions;

namespace Zebl.Api.Services;

/// <summary>
/// Request-scoped tenant (from <c>X-Tenant-Key</c> via <see cref="ITenantContext"/>) and facility (from <c>X-Facility-Id</c>).
/// </summary>
public sealed class HeaderCurrentContext : ICurrentContext
{
    private const string FacilityHeader = "X-Facility-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public HeaderCurrentContext(IHttpContextAccessor httpContextAccessor, ITenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public int TenantId => _tenantContext.TenantId;

    public int FacilityId
    {
        get
        {
            var http = _httpContextAccessor.HttpContext;
            var headers = http?.Request?.Headers;
            if (headers == null)
                throw new InvalidOperationException("Facility context is unavailable for this request.");

            if (headers.TryGetValue(FacilityHeader, out var values))
            {
                var raw = values.ToString();
                if (!int.TryParse(raw, out var facilityId) || facilityId <= 0)
                    throw new InvalidOperationException("X-Facility-Id must be a positive integer.");
                return facilityId;
            }

            throw new InvalidOperationException("X-Facility-Id header is required.");
        }
    }
}
