namespace Zebl.Application.Domain;

/// <summary>
/// Domain entity for EDI Report (operational tracking). No EF attributes.
/// </summary>
public class EdiReport
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }
    public Guid? ReceiverLibraryId { get; set; }
    public Guid? ConnectionLibraryId { get; set; }
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;   // "837", "270", "835", "999", "CSR"
    public string Direction { get; set; } = null!;  // "Outbound", "Inbound"
    public string Status { get; set; } = null!;     // "Generated", "Sent", "Received", "Failed"
    public string? TraceNumber { get; set; }
    public string? ClaimIdentifier { get; set; }
    public string? PayerName { get; set; }
    public decimal? PaymentAmount { get; set; }
    public string? Note { get; set; }               // Max 255 characters
    public bool IsArchived { get; set; }
    public bool IsRead { get; set; }
    public long FileSize { get; set; }

    /// <summary>Logical key for file on disk/blob store (required for all reports).</summary>
    public string FileStorageKey { get; set; } = null!;

    /// <summary>SHA-256 of file bytes (hex), used for idempotent inbound deduplication.</summary>
    public string? ContentHashSha256 { get; set; }
    public string? FileHash { get; set; }
    public string CorrelationId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
}
