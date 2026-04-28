using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zebl.Application.Services;

namespace Zebl.Api.HealthChecks;

public sealed class EdiStorageHealthCheck : IHealthCheck
{
    private readonly IEdiReportFileStore _fileStore;

    public EdiStorageHealthCheck(IEdiReportFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var key = $"health/{Guid.NewGuid():N}.tmp";
        var payload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });
        try
        {
            await _fileStore.WriteAsync(key, payload, cancellationToken).ConfigureAwait(false);
            var exists = await _fileStore.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
            await _fileStore.TryDeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return exists
                ? HealthCheckResult.Healthy("EDI storage reachable.")
                : HealthCheckResult.Unhealthy("EDI storage write succeeded but file not found.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("EDI storage unavailable.", ex);
        }
    }
}

