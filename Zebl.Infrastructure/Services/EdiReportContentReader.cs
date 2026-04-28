using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

public sealed class EdiReportContentReader : IEdiReportContentReader
{
    private readonly IEdiReportFileStore _fileStore;

    public EdiReportContentReader(IEdiReportFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public async Task<byte[]> ReadAllBytesAsync(EdiReport report, CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenReadAsync(report, cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    public async Task<Stream> OpenReadAsync(EdiReport report, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report.FileStorageKey))
            throw new EdiReportFileNotAvailableException($"EDI report {report.Id} has no file storage key.");

        var stream = await _fileStore.OpenReadAsync(report.FileStorageKey, cancellationToken).ConfigureAwait(false);
        if (stream == null)
            throw new EdiReportFileNotAvailableException($"EDI file bytes missing in storage for report {report.Id}.");
        if (stream.CanSeek && stream.Length == 0)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new EdiReportFileNotAvailableException($"EDI file bytes missing in storage for report {report.Id}.");
        }

        return stream;
    }
}
