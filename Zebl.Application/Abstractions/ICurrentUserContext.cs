namespace Zebl.Application.Abstractions;

/// <summary>
/// Provides current user/computer for audit (Created/Modified).
/// Replace with JWT-backed implementation when auth is wired.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? ComputerName { get; }
}
