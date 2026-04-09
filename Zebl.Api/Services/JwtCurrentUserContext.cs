using Microsoft.AspNetCore.Http;
using Zebl.Application.Abstractions;

namespace Zebl.Api.Services;

/// <summary>
/// Resolves user and tenant from JWT claims when authenticated; otherwise SYSTEM with TenantId = 0.
/// Never throws in the constructor — tenant enforcement belongs in business logic.
/// </summary>
public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");

    private readonly Guid? _userId;
    private readonly string? _userName;
    private readonly int _tenantId;

    public JwtCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            _userId = null;
            _userName = "SYSTEM";
            _tenantId = 0;
            return;
        }

        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst("UserGuid")?.Value;

        Guid.TryParse(sub, out var guid);
        _userId = guid == Guid.Empty ? null : guid;

        _userName = user.Identity?.Name ?? "UNKNOWN";

        var tenantClaim = user.FindFirst("tenantId")?.Value;
        _tenantId = int.TryParse(tenantClaim, out var tid) ? tid : 0;
    }

    public Guid? UserId => _userId;

    public string? UserName => _userName;

    public int TenantId => _tenantId;

    public string? ComputerName
    {
        get
        {
            var machineName = Environment.MachineName;
            return string.IsNullOrWhiteSpace(machineName) ? "SERVER" : machineName;
        }
    }
}
