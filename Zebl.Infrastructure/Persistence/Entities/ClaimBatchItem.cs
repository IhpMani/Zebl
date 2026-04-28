namespace Zebl.Infrastructure.Persistence.Entities;

public class ClaimBatchItem : ITenantFacilityEntity
{
    public int Id { get; set; }
    public Guid BatchId { get; set; }
    public int ClaimId { get; set; }
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ClaimBatch Batch { get; set; } = null!;
}
