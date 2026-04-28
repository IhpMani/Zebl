using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Resolves full EDI bytes from external storage for a report.
/// </summary>
public interface IEdiReportContentReader
{
    /// <exception cref="EdiReportFileNotAvailableException">Missing storage key or file bytes.</exception>
    Task<byte[]> ReadAllBytesAsync(EdiReport report, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(EdiReport report, CancellationToken cancellationToken = default);
}
