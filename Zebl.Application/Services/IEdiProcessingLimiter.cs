namespace Zebl.Application.Services;

public interface IEdiProcessingLimiter
{
    Task<IAsyncDisposable> AcquireInboundSlotAsync(CancellationToken cancellationToken = default);
    EdiProcessingLimiterSnapshot GetSnapshot();
}

public sealed record EdiProcessingLimiterSnapshot(int MaxConcurrency, int CurrentInUse, int QueueDepth);

