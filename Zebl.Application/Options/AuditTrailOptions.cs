namespace Zebl.Application.Options;

public sealed class AuditTrailOptions
{
    /// <summary>Secret for audit entry hashing; should be long and random in production.</summary>
    public string IntegritySecret { get; set; } = string.Empty;
}
