using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zebl.Application.Services;

namespace Zebl.Api.HealthChecks;

public sealed class EdiQueueHealthCheck : IHealthCheck
{
    private readonly IEdiProcessingLimiter _limiter;

    public EdiQueueHealthCheck(IEdiProcessingLimiter limiter)
    {
        _limiter = limiter;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = _limiter.GetSnapshot();
        if (snapshot.QueueDepth > snapshot.MaxConcurrency * 10)
            return Task.FromResult(HealthCheckResult.Degraded($"EDI queue depth high ({snapshot.QueueDepth})."));
        return Task.FromResult(HealthCheckResult.Healthy($"EDI queue depth {snapshot.QueueDepth}, in-use {snapshot.CurrentInUse}."));
    }
}

