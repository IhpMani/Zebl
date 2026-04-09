using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Resolves inbound integration scope from X-Integration-Id header.
/// No fallback is allowed for imports.
/// </summary>
public sealed class HeaderInboundContext : IInboundContext
{
    // TODO: Replace header-based integration identity with API key or mTLS binding.
    private const string IntegrationHeader = "X-Integration-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ZeblDbContext _db;
    private InboundIntegration? _resolved;

    public HeaderInboundContext(IHttpContextAccessor httpContextAccessor, ZeblDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public int TenantId => Resolve().TenantId;
    public int FacilityId => Resolve().FacilityId;
    public int IntegrationId => Resolve().Id;

    private InboundIntegration Resolve()
    {
        if (_resolved != null)
            return _resolved;

        var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
        if (headers == null || !headers.TryGetValue(IntegrationHeader, out var values))
            throw new InvalidOperationException("X-Integration-Id header is required.");

        var raw = values.ToString();
        if (!int.TryParse(raw, out var integrationId) || integrationId <= 0)
            throw new InvalidOperationException("X-Integration-Id must be a positive integer.");

        var integration = _db.InboundIntegrations
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == integrationId && i.IsActive);
        if (integration == null)
            throw new InvalidOperationException("X-Integration-Id does not map to an active inbound integration.");

        _resolved = integration;
        return _resolved;
    }
}
