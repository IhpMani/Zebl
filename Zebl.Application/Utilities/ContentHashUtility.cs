using System.Security.Cryptography;
using System.Text;

namespace Zebl.Application.Utilities;

public static class ContentHashUtility
{
    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash);
    }

    public static string Sha256HexFromUtf8(string text)
        => Sha256Hex(Encoding.UTF8.GetBytes(text));
}
