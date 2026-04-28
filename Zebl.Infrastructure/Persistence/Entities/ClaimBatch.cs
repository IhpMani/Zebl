namespace Zebl.Infrastructure.Persistence.Entities;

public class ClaimBatch : ITenantFacilityEntity
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string Status { get; set; } = null!;
    public int TotalClaims { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? FilePath { get; set; }
    public string? SentEdiContent { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int SubmissionNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? SubmitterReceiverId { get; set; }
    public string? ConnectionType { get; set; }
    public Guid? ConnectionLibraryId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<ClaimBatchItem> Items { get; set; } = new List<ClaimBatchItem>();
}
