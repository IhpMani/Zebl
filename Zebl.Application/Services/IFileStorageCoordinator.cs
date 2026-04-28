namespace Zebl.Application.Services;

/// <summary>
/// Coordinates external file storage and DB persistence to avoid orphaned records/files.
/// </summary>
public interface IFileStorageCoordinator
{
    Task PersistWithDbAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string correlationId,
        Func<CancellationToken, Task> persistDbAction,
        CancellationToken cancellationToken = default);
}

