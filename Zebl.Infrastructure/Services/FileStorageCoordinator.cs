using Microsoft.Extensions.Logging;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

public sealed class FileStorageCoordinator : IFileStorageCoordinator
{
    private readonly IEdiReportFileStore _fileStore;
    private readonly ILogger<FileStorageCoordinator> _logger;

    public FileStorageCoordinator(
        IEdiReportFileStore fileStore,
        ILogger<FileStorageCoordinator> logger)
    {
        _fileStore = fileStore;
        _logger = logger;
    }

    public async Task PersistWithDbAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string correlationId,
        Func<CancellationToken, Task> persistDbAction,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File storage persist start. CorrelationId={CorrelationId} StorageKey={StorageKey}", correlationId, storageKey);
        await _fileStore.WriteAsync(storageKey, content, cancellationToken).ConfigureAwait(false);

        var attempts = 0;
        Exception? last = null;
        while (attempts < 3)
        {
            attempts++;
            try
            {
                await persistDbAction(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempts < 3)
            {
                last = ex;
                EdiOperationalMetrics.RetryCount.Add(
                    1,
                    new KeyValuePair<string, object?>("operation", "persist-with-db"),
                    new KeyValuePair<string, object?>("outcome", "retry"));
                _logger.LogWarning(ex, "DB persist attempt {Attempt} failed for storage key {StorageKey}. CorrelationId={CorrelationId}. Retrying.", attempts, storageKey, correlationId);
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempts), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        await _fileStore.TryDeleteAsync(storageKey, cancellationToken).ConfigureAwait(false);
        EdiOperationalMetrics.FailureCount.Add(
            1,
            new KeyValuePair<string, object?>("operation", "persist-with-db"),
            new KeyValuePair<string, object?>("outcome", "failed"));
        _logger.LogError(last, "File storage persist failed and rolled back file. CorrelationId={CorrelationId} StorageKey={StorageKey}", correlationId, storageKey);
        throw new InvalidOperationException(
            $"Failed to persist EDI report after writing file to storage key {storageKey}. File was rolled back.",
            last);
    }
}

