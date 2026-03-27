namespace Zebl.Application.Domain;

/// <summary>
/// Single source of truth for allowed claim statuses and display labels for API/UI.
/// </summary>
public static class ClaimStatusCatalog
{
    private static readonly (ClaimStatus Status, string DisplayName)[] Items =
    [
        (ClaimStatus.OnHold, "On Hold"),
        (ClaimStatus.Submitted, "Submitted"),
        (ClaimStatus.RTS, "RTS"),
        (ClaimStatus.Other, "Other")
    ];

    public static IReadOnlyList<(ClaimStatus Status, string DisplayName)> All => Items;

    public static bool TryParse(string? text, out ClaimStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Enum.TryParse(text.Trim(), ignoreCase: false, out status);
    }

    public static string ToStorage(ClaimStatus status) => status.ToString();

    public static bool IsValidStoredValue(string? text) => TryParse(text, out _);
}
