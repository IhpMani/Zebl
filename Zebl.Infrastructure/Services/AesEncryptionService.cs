using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// AES encryption service implementation. Uses AES-256-CBC encryption.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly string _encryptionKey;
    private readonly byte[] _keyBytes;
    private readonly byte[] _ivBytes;

    public AesEncryptionService(IConfiguration configuration)
    {
        _encryptionKey = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException("Encryption:Key is not configured in appsettings.json");

        if (string.IsNullOrWhiteSpace(_encryptionKey) || _encryptionKey.Length < 32)
        {
            throw new InvalidOperationException("Encryption:Key must be at least 32 characters long.");
        }

        // Derive 32-byte key and 16-byte IV from the configuration key
        using var sha256 = SHA256.Create();
        _keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_encryptionKey));
        
        // Use first 16 bytes of key hash as IV (for simplicity, in production use random IV per encryption)
        _ivBytes = _keyBytes.Take(16).ToArray();
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _keyBytes;
        aes.IV = _ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _keyBytes;
            aes.IV = _ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            throw new InvalidOperationException("Failed to decrypt password. The encryption key may have changed.");
        }
    }
}
