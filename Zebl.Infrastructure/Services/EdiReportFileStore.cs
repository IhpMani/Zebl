using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Stores generated EDI file content on disk under a temp folder. Implemented in Infrastructure.
/// </summary>
public class EdiReportFileStore : IEdiReportFileStore
{
    private static readonly string _basePath = Path.Combine(Path.GetTempPath(), "ZeblEdiReports");

    static EdiReportFileStore()
    {
        Directory.CreateDirectory(_basePath);
    }

    private string GetPath(Guid reportId) => Path.Combine(_basePath, $"{reportId:N}.edi");

    public Task SaveContentAsync(Guid reportId, string content)
    {
        var path = GetPath(reportId);
        File.WriteAllText(path, content);
        return Task.CompletedTask;
    }

    public Task<string?> GetContentAsync(Guid reportId)
    {
        var path = GetPath(reportId);
        if (!File.Exists(path))
            return Task.FromResult<string?>(null);
        var content = File.ReadAllText(path);
        return Task.FromResult<string?>(content);
    }

    public Task TryDeleteAsync(Guid reportId)
    {
        var path = GetPath(reportId);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
