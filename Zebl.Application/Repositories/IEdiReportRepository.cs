using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

/// <summary>
/// Repository for EdiReport. Application layer abstraction; implemented in Infrastructure.
/// </summary>
public interface IEdiReportRepository
{
    Task<List<EdiReport>> GetAllAsync(bool? isArchived = null);
    Task<EdiReport?> GetByIdAsync(Guid id);
    Task<int> DeleteByReceiverAndConnectionAsync(Guid receiverLibraryId, Guid? connectionLibraryId);
    Task AddAsync(EdiReport report);
    Task UpdateAsync(EdiReport report);
    Task DeleteAsync(Guid id);
}
