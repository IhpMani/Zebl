using Zebl.Application.Services;

namespace Zebl.Api.Services;

public sealed class EdiMetricsReporterService : BackgroundService
{
    private readonly IEdiProcessingLimiter _limiter;
    private readonly ILogger<EdiMetricsReporterService> _logger;

    public EdiMetricsReporterService(
        IEdiProcessingLimiter limiter,
        ILogger<EdiMetricsReporterService> logger)
    {
        _limiter = limiter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            using var _ambient = CorrelationContext.Push(correlationId);
            using var _scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
            var snapshot = _limiter.GetSnapshot();
            _logger.LogInformation(
                "EDI metrics snapshot. CorrelationId={CorrelationId} MaxConcurrency={MaxConcurrency} InUse={InUse} QueueDepth={QueueDepth}",
                correlationId,
                snapshot.MaxConcurrency,
                snapshot.CurrentInUse,
                snapshot.QueueDepth);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}

