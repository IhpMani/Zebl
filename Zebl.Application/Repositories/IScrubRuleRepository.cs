using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IScrubRuleRepository
{
    Task<List<ScrubRule>> GetActiveAsync(int? payerId, int? programId);
}

