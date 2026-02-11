using System.Security.Cryptography;

namespace Zebl.Api.Services;

/// <summary>
/// PBKDF2 (HMAC-SHA256) password hashing. No ASP.NET Identity.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int IterationCount = 100_000;

    /// <summary>
    /// Hash a password and return hash + salt for storage.
    /// </summary>
    public static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = new byte[SaltSizeBytes];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);

        var hash = Pbkdf2(password, salt, HashSizeBytes, IterationCount);
        return (hash, salt);
    }

    /// <summary>
    /// Verify password against stored hash and salt.
    /// </summary>
    public static bool VerifyPassword(string password, byte[]? storedHash, byte[]? storedSalt)
    {
        if (string.IsNullOrEmpty(password) || storedHash == null || storedHash.Length == 0 ||
            storedSalt == null || storedSalt.Length == 0)
            return false;

        var computedHash = Pbkdf2(password, storedSalt, HashSizeBytes, IterationCount);
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int outputBytes, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(outputBytes);
    }
}
