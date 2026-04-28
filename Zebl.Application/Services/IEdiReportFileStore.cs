namespace Zebl.Application.Services;

/// <summary>
/// Persists EDI payload bytes outside the SQL row (disk or future blob provider).
/// </summary>
public interface IEdiReportFileStore
{
    /// <summary>Relative storage key (e.g. tenantId/reportId.edi).</summary>
    string BuildStorageKey(int tenantId, Guid reportId, string fileName);

    Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> EnumerateStorageKeysAsync(CancellationToken cancellationToken = default);

    Task TryDeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
