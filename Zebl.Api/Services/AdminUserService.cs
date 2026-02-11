using Microsoft.Extensions.Configuration;

namespace Zebl.Api.Services;

public interface IAdminUserService
{
    bool IsAdminUser(string? userName);
}

/// <summary>
/// Minimal admin mechanism without schema changes.
/// Admin users are configured via appsettings: AdminUsers: [ \"admin\", ... ].
/// </summary>
public sealed class AdminUserService : IAdminUserService
{
    private readonly HashSet<string> _adminUsers;

    public AdminUserService(IConfiguration configuration)
    {
        var list = configuration.GetSection("AdminUsers").Get<string[]>() ?? Array.Empty<string>();
        _adminUsers = new HashSet<string>(
            list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdminUser(string? userName)
        => !string.IsNullOrWhiteSpace(userName) && _adminUsers.Contains(userName.Trim());
}

