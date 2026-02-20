namespace Zebl.Application.Services;

/// <summary>
/// Encryption service interface. Application layer abstraction for encryption operations.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plain text and returns Base64 encoded cipher text.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts Base64 encoded cipher text and returns plain text.
    /// </summary>
    string Decrypt(string cipherText);
}
