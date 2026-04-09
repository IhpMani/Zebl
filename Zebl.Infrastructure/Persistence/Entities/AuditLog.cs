namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>Security and compliance audit trail (append-only).</summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    public string Action { get; set; } = null!;

    public Guid? UserId { get; set; }

    public int? TenantId { get; set; }

    public DateTime TimestampUtc { get; set; }

    /// <summary>Serialized <see cref="Zebl.Application.Dtos.Common.AuditMetadata"/> only.</summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>SHA-256 hex of integrity payload + secret (tamper detection). Null for pre-integrity rows.</summary>
    public string? Hash { get; set; }

    /// <summary>Prior hash in chain (optional).</summary>
    public string? PreviousHash { get; set; }
}

