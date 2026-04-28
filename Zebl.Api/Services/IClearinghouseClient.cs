using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

public interface IClearinghouseClient
{
    Task<SubmissionResult> SubmitBatchAsync(ClaimBatch batch, string ediContent);
    Task<SubmissionResult> UploadEligibilityAsync(string fileName, string ediContent, CancellationToken cancellationToken = default);
}

public sealed class SubmissionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
