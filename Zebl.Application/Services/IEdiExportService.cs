namespace Zebl.Application.Services;

public interface IEdiExportService
{
    Task<string> GenerateAsync(Guid receiverLibraryId, int claimId);

    /// <summary>
    /// Generate an ANSI 270 eligibility request for the given receiver.
    /// Does not depend on a specific claim.
    /// </summary>
    Task<string> Generate270Async(Guid receiverLibraryId);
}
