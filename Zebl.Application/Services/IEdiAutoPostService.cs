namespace Zebl.Application.Services;

public interface IEdiAutoPostService
{
    Task<EdiAutoPostResult> Apply835Async(Guid reportId, string correlationId, string postedBy, CancellationToken cancellationToken = default);
}

/// <summary>835 auto-post outcome metrics (idempotent apply run).</summary>
public sealed record EdiAutoPostResult(
    int Processed,
    int Applied,
    int DuplicatesSkipped,
    int Unmatched,
    int Reversed,
    int CreditsCreated,
    int Invalid,
    int Skipped);
