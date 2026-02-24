using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

/// <summary>
/// Repository for Payer. Application layer abstraction; implemented in Infrastructure.
/// </summary>
public interface IPayerRepository
{
    Task<List<Payer>> GetAllAsync(bool includeInactive = false);
    Task<(List<Payer> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, bool includeInactive, string? classificationList = null);
    Task<Payer?> GetByIdAsync(int id);
    Task<List<Payer>> GetByMatchingKeyAsync(string key);
    /// <summary>Returns all payers that share the same payment matching key (same logical payer, e.g. BCBS variants).</summary>
    Task<List<Payer>> GetEquivalentPayersByMatchingKeyAsync(string key);
    /// <summary>Returns payers by external ID (e.g. from 835); multiple possible.</summary>
    Task<List<Payer>> GetByExternalIdAsync(string? externalId);
    Task<bool> IsInUseAsync(int payId);
    Task<Payer> AddAsync(Payer entity);
    Task UpdateAsync(Payer entity);
    Task DeleteAsync(int id);
}
