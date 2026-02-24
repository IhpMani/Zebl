namespace Zebl.Application.Repositories;

/// <summary>
/// Logs interface import events (e.g. ERA partial failure) to Interface_Import_Log. Implemented in Infrastructure.
/// </summary>
public interface IImportLogRepository
{
    /// <summary>
    /// Logs an ERA import note (e.g. "No payer match for ..."). Does not throw.
    /// </summary>
    Task LogEraImportAsync(string fileName, string notes);
}
