using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Zebl.Application.Abstractions;

namespace Zebl.Api.Services;

/// <summary>
/// JWT present → real user from claims (UserGuid, UserName).
/// JWT missing or not authenticated → SYSTEM user.
/// </summary>
public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");
    private const string SystemUserName = "SYSTEM";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return SystemUserId;

            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? user.FindFirstValue("sub")
                       ?? user.FindFirstValue("UserGuid");
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var guid))
                return SystemUserId;
            return guid;
        }
    }

    public string? UserName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return SystemUserName;

            var name = user.FindFirstValue(ClaimTypes.Name)
                       ?? user.FindFirstValue("UserName")
                       ?? user.FindFirstValue("unique_name");
            return string.IsNullOrEmpty(name) ? SystemUserName : name;
        }
    }

    public string? ComputerName
    {
        get
        {
            // Always return server machine name (never null/empty).
            // This ensures audit fields are always populated globally.
            var machineName = Environment.MachineName;
            return string.IsNullOrWhiteSpace(machineName) ? "SERVER" : machineName;
        }
    }
}
