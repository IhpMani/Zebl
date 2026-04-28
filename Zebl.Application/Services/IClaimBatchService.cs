namespace Zebl.Application.Services;

public interface IClaimBatchService
{
    Task<BatchCreationResult> CreateBatchAsync(CreateBatchRequest request, CancellationToken cancellationToken);
    Task<BatchProcessResult> ProcessBatchAsync(ProcessBatchRequest request, CancellationToken cancellationToken);
    Task<BatchProcessResult> RetryBatchAsync(RetryBatchRequest request, CancellationToken cancellationToken);
    Task<BatchDetailResult?> GetBatchAsync(GetBatchRequest request, CancellationToken cancellationToken);
    Task<BatchListResult> GetBatchesAsync(GetBatchesRequest request, CancellationToken cancellationToken);
    Task<BatchEdiResult> GetBatchEdiAsync(GetBatchRequest request, CancellationToken cancellationToken);
    Task<BatchZipResult> ExportBatchZipAsync(GetBatchRequest request, CancellationToken cancellationToken);
}

public sealed class CreateBatchRequest
{
    public IReadOnlyList<int> ClaimIds { get; set; } = [];
    public bool ForceResubmit { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? SubmitterReceiverId { get; set; }
    public string? ConnectionType { get; set; }
    public Guid? ConnectionLibraryId { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class BlockedClaimResult
{
    public int ClaimId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class FailedClaimResult
{
    public int ClaimId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class BatchCreationResult
{
    public Guid BatchId { get; set; }
    public bool IsIdempotentHit { get; set; }
    public IReadOnlyList<BlockedClaimResult> BlockedClaims { get; set; } = [];
    public int TotalRequestedClaims { get; set; }
}

public sealed class ProcessBatchRequest
{
    public Guid BatchId { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
}

public sealed class RetryBatchRequest
{
    public Guid BatchId { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
}

public sealed class BatchProcessResult
{
    public Guid BatchId { get; set; }
    public int TotalClaims { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public IReadOnlyList<FailedClaimResult> FailedClaims { get; set; } = [];
}

public sealed class GetBatchRequest
{
    public Guid BatchId { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
}

public sealed class GetBatchesRequest
{
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class BatchListItemResult
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SubmissionNumber { get; set; }
    public Guid? SubmitterReceiverId { get; set; }
    public string? ConnectionType { get; set; }
    public int TotalClaims { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? FilePath { get; set; }
}

public sealed class BatchListResult
{
    public IReadOnlyList<BatchListItemResult> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class BatchItemResult
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BatchDetailResult
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SubmissionNumber { get; set; }
    public Guid? SubmitterReceiverId { get; set; }
    public string? ConnectionType { get; set; }
    public int TotalClaims { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? FilePath { get; set; }
    public IReadOnlyList<BatchItemResult> Items { get; set; } = [];
}

public sealed class BatchEdiResult
{
    public Guid BatchId { get; set; }
    public string EdiContent { get; set; } = string.Empty;
}

public sealed class BatchZipResult
{
    public Guid BatchId { get; set; }
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
}
