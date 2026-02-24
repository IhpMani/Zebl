using Zebl.Application.Dtos.Claims;

namespace Zebl.Application.Services;

/// <summary>
/// Provides all data required to generate an 837 for a claim. Implemented in Infrastructure.
/// </summary>
public interface IClaimExportDataProvider
{
    Task<Claim837ExportData?> GetExportDataAsync(int claimId);
}
