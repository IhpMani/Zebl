using Zebl.Application.Abstractions;

namespace Zebl.Api.Services;

/// <summary>
/// Temporary audit user until JWT/auth is wired. All writes are attributed to SYSTEM.
/// </summary>
public sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");

    public Guid? UserId => SystemUserId;
    public string? UserName => "SYSTEM";
    
    public string? ComputerName
    {
        get
        {
            // Always return server machine name (never null/empty).
            var machineName = Environment.MachineName;
            return string.IsNullOrWhiteSpace(machineName) ? "SERVER" : machineName;
        }
    }
}
