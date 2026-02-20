namespace Zebl.Application.Services;

/// <summary>
/// Abstraction for storing/retrieving generated EDI file content (temp file system).
/// Implemented in Infrastructure.
/// </summary>
public interface IEdiReportFileStore
{
    Task SaveContentAsync(Guid reportId, string content);
    Task<string?> GetContentAsync(Guid reportId);
    Task TryDeleteAsync(Guid reportId);
}
