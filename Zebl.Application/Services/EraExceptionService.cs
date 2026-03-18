using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

public class EraExceptionService
{
    private readonly IEraExceptionRepository _repository;

    public EraExceptionService(IEraExceptionRepository repository)
    {
        _repository = repository;
    }

    public Task<List<EraException>> GetOpenExceptionsAsync()
        => _repository.GetOpenAsync();

    public Task<EraException?> GetExceptionByIdAsync(int id)
        => _repository.GetByIdAsync(id);

    public async Task AssignExceptionAsync(int id, int userId)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return;
        existing.AssignedUserId = userId;
        if (existing.Status == "Open")
            existing.Status = "InProgress";
        await _repository.UpdateAsync(existing);
    }

    public async Task ResolveExceptionAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return;
        existing.Status = "Resolved";
        existing.ResolvedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(existing);
    }

    public async Task<int> CreateExceptionAsync(
        Guid ediReportId,
        int? claimId,
        int? serviceLineId,
        string exceptionType,
        string message,
        string eraClaimIdentifier)
    {
        var entity = new EraException
        {
            EdiReportId = ediReportId,
            ClaimId = claimId,
            ServiceLineId = serviceLineId,
            ExceptionType = exceptionType,
            Message = message,
            EraClaimIdentifier = eraClaimIdentifier,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(entity);
        return entity.Id;
    }
}

