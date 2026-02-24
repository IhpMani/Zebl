using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Application service for Payer Library. Enforces business rules; no DbContext, no EF.
/// </summary>
public class PayerService
{
    private readonly IPayerRepository _repository;

    public PayerService(IPayerRepository repository)
    {
        _repository = repository;
    }

    public Task<List<Payer>> GetAllAsync(bool includeInactive = false) =>
        _repository.GetAllAsync(includeInactive);

    public Task<(List<Payer> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, bool includeInactive, string? classificationList = null) =>
        _repository.GetPagedAsync(page, pageSize, includeInactive, classificationList);

    public Task<Payer?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    /// <summary>
    /// RULE 3 – Payment Matching Key: payers sharing the same key are treated as equivalent (e.g. for 835 auto-post).
    /// </summary>
    public Task<List<Payer>> GetPayersByMatchingKeyAsync(string key) =>
        _repository.GetByMatchingKeyAsync(key ?? string.Empty);

    /// <summary>
    /// RULE 1 – Payer ID required for Electronic. Validates before add/update.
    /// </summary>
    public void ValidateForSave(Payer payer)
    {
        if (payer == null)
            throw new ArgumentNullException(nameof(payer));

        if (string.Equals(payer.PaySubmissionMethod, "Electronic", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(payer.PayExternalID))
                throw new InvalidOperationException("Payer ID is required when Method is Electronic.");
        }
    }

    public async Task<Payer> CreateAsync(Payer payer)
    {
        ValidateForSave(payer);
        payer.PayID = 0;
        payer.PayDateTimeCreated = DateTime.UtcNow;
        payer.PayDateTimeModified = DateTime.UtcNow;
        var created = await _repository.AddAsync(payer);
        return created;
    }

    public async Task UpdateAsync(Payer payer)
    {
        if (payer == null)
            throw new ArgumentNullException(nameof(payer));
        var existing = await _repository.GetByIdAsync(payer.PayID)
            ?? throw new InvalidOperationException("Payer not found.");
        ValidateForSave(payer);
        payer.PayDateTimeCreated = existing.PayDateTimeCreated;
        payer.PayDateTimeModified = DateTime.UtcNow;
        await _repository.UpdateAsync(payer);
    }

    /// <summary>
    /// RULE 2 – Cannot delete payer if in use. Use Inactive instead.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var inUse = await _repository.IsInUseAsync(id);
        if (inUse)
            throw new InvalidOperationException("Payer cannot be deleted because it is in use. Use Inactive instead.");
        await _repository.DeleteAsync(id);
    }

    public Task<bool> IsInUseAsync(int id) => _repository.IsInUseAsync(id);

    /// <summary>
    /// RULE 4 – Placeholder: when 835 status = "Processed as Primary, Forwarded" and PayForwardsClaims == true,
    /// set secondary claim status = Submitted; else ReadyToSubmit.
    /// </summary>
    public Task HandleForwardingLogicAsync(int claimId, int payerId, string eraStatus)
    {
        // Placeholder for auto-post engine integration.
        return Task.CompletedTask;
    }
}
