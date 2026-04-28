using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Stores EDI payloads under a configurable root directory (tenant-scoped subfolders).
/// </summary>
public sealed class EdiReportFileStore : IEdiReportFileStore
{
    private readonly string _root;

    public EdiReportFileStore(string rootDirectory)
    {
        _root = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        Directory.CreateDirectory(_root);
    }

    public string BuildStorageKey(int tenantId, Guid reportId, string fileName)
    {
        var baseName = string.IsNullOrWhiteSpace(fileName) ? "file.edi" : Path.GetFileName(fileName);
        var safe = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrEmpty(safe)) safe = "file.edi";
        return $"{tenantId}/{reportId:N}_{safe}";
    }

    public async Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = PhysicalPath(storageKey);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 64,
            useAsync: true);
        await fs.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PhysicalPath(storageKey);
        if (!File.Exists(path))
            return null;
        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PhysicalPath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PhysicalPath(storageKey);
        return Task.FromResult(File.Exists(path));
    }

    public async IAsyncEnumerable<string> EnumerateStorageKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root))
            yield break;

        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
            yield return relative;
            await Task.Yield();
        }
    }

    public Task TryDeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PhysicalPath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string PhysicalPath(string storageKey)
    {
        var parts = storageKey.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(new[] { _root }.Concat(parts).ToArray());
    }
}
