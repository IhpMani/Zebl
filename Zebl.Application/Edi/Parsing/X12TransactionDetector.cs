namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Identifies X12 transaction type from the first ST segment (ST01), without substring scans on raw content.
/// </summary>
public static class X12TransactionDetector
{
    public static readonly HashSet<string> SupportedTransactionSetIds = new(StringComparer.Ordinal)
    {
        "837",
        "835",
        "270",
        "999"
    };

    /// <summary>Returns ST01 (e.g. 837, 835, 270, 999) or null if no ST segment exists.</summary>
    public static string? TryGetTransactionIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        foreach (var seg in X12Tokenizer.Enumerate(raw))
        {
            if (seg.Id == "ST" && seg.Elements.Count > 1)
                return seg.Elements[1]?.Trim();
        }

        return null;
    }

    public static async Task<string?> TryGetTransactionIdentifierAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await foreach (var seg in X12Tokenizer.EnumerateAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            if (seg.Id == "ST" && seg.Elements.Count > 1)
                return seg.Elements[1]?.Trim();
        }

        return null;
    }

    public static bool IsSupported(string? st01)
    {
        if (string.IsNullOrWhiteSpace(st01))
            return false;
        return SupportedTransactionSetIds.Contains(st01.Trim());
    }
}
