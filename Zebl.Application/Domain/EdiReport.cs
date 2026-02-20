namespace Zebl.Application.Domain;

/// <summary>
/// Domain entity for EDI Report (operational tracking). No EF attributes.
/// </summary>
public class EdiReport
{
    public Guid Id { get; set; }
    public Guid? ReceiverLibraryId { get; set; }
    public Guid? ConnectionLibraryId { get; set; }
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;   // "837", "270", "835", "999", "CSR"
    public string Direction { get; set; } = null!;  // "Outbound", "Inbound"
    public string Status { get; set; } = null!;     // "Generated", "Sent", "Received", "Failed"
    public string? TraceNumber { get; set; }
    public string? PayerName { get; set; }
    public decimal? PaymentAmount { get; set; }
    public string? Note { get; set; }               // Max 255 characters
    public bool IsArchived { get; set; }
    public bool IsRead { get; set; }
    public long FileSize { get; set; }
    public byte[]? FileContent { get; set; }        // Binary content stored in DB
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
}
