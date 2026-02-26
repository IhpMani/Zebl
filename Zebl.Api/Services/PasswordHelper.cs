using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Helper for creating AppUser instances with hashed passwords (no ASP.NET Identity).
/// Intended for manual/seeding scenarios only.
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// Create a new active AppUser with a hashed password.
    /// </summary>
    public static AppUser CreateUser(string userName, string? email, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("User name is required.", nameof(userName));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.", nameof(password));

        var (hash, salt) = PasswordHasher.HashPassword(password);

        return new AppUser
        {
            UserGuid = Guid.NewGuid(),
            UserName = userName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = hash,
            PasswordSalt = salt
        };
    }
}

