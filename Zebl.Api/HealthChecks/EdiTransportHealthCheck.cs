using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zebl.Application.Domain;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.HealthChecks;

public sealed class EdiTransportHealthCheck : IHealthCheck
{
    private readonly ZeblDbContext _db;

    public EdiTransportHealthCheck(ZeblDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var activeConnections = await _db.ConnectionLibraries
            .AsNoTracking()
            .Where(c => c.IsActive && (c.ConnectionType == ConnectionType.Sftp || c.ConnectionType == ConnectionType.Http || c.ConnectionType == ConnectionType.Api))
            .Select(c => new { c.Id, c.Host })
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeConnections.Count == 0)
            return HealthCheckResult.Degraded("No active EDI transport connections configured.");

        var invalidHosts = activeConnections.Count(c => string.IsNullOrWhiteSpace(c.Host));
        if (invalidHosts > 0)
            return HealthCheckResult.Unhealthy($"Found {invalidHosts} active EDI connections with missing host.");

        return HealthCheckResult.Healthy($"Validated {activeConnections.Count} active EDI connections.");
    }
}

