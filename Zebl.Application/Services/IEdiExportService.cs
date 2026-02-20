namespace Zebl.Application.Services;

public interface IEdiExportService
{
    Task<string> GenerateAsync(Guid receiverLibraryId, int claimId);
}
