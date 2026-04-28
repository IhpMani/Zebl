using Microsoft.EntityFrameworkCore;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Services;

public sealed class EdiOrphanReconciliationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EdiOrphanReconciliationService> _logger;

    public EdiOrphanReconciliationService(
        IServiceScopeFactory scopeFactory,
        ILogger<EdiOrphanReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            using var _ambient = CorrelationContext.Push(correlationId);
            using var _scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
            try
            {
                await ReconcileOnceAsync(correlationId, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EDI orphan reconciliation failed. CorrelationId={CorrelationId}", correlationId);
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileOnceAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZeblDbContext>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IEdiReportFileStore>();

        _logger.LogInformation("EDI orphan reconciliation start. CorrelationId={CorrelationId}", correlationId);

        var reportKeys = await db.EdiReports
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => !string.IsNullOrEmpty(r.FileStorageKey))
            .Select(r => r.FileStorageKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var reportKeySet = new HashSet<string>(reportKeys, StringComparer.OrdinalIgnoreCase);

        var deletedOrphanFiles = 0;
        await foreach (var key in fileStore.EnumerateStorageKeysAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reportKeySet.Contains(key))
                continue;
            await fileStore.TryDeleteAsync(key, cancellationToken).ConfigureAwait(false);
            deletedOrphanFiles++;
        }

        var dbRecordsWithoutFiles = 0;
        var reports = await db.EdiReports
            .IgnoreQueryFilters()
            .Where(r => !string.IsNullOrEmpty(r.FileStorageKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var report in reports)
        {
            var exists = await fileStore.ExistsAsync(report.FileStorageKey, cancellationToken).ConfigureAwait(false);
            if (exists)
                continue;
            dbRecordsWithoutFiles++;
            report.Status = "Failed";
            var marker = "Missing file reconciled";
            report.Note = string.IsNullOrWhiteSpace(report.Note) ? marker : $"{report.Note}; {marker}";
        }

        if (dbRecordsWithoutFiles > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "EDI orphan reconciliation complete. CorrelationId={CorrelationId} OrphanFilesDeleted={OrphanFilesDeleted} DbRecordsMarkedFailed={DbRecordsMarkedFailed}",
            correlationId,
            deletedOrphanFiles,
            dbRecordsWithoutFiles);
    }
}

