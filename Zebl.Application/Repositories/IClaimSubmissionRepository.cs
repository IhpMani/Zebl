using Zebl.Application.Domain;

namespace Zebl.Application.Repositories;

public interface IClaimSubmissionRepository
{
    /// <summary>Returns next unique transaction set control number (4–9 digits) for 837 ST02.</summary>
    Task<string> GetNextTransactionControlNumberAsync();

    Task AddAsync(ClaimSubmission entity);

    /// <summary>Resolve ClaimId from 999 AK2 TransactionControlNumber.</summary>
    Task<ClaimSubmission?> GetByTransactionControlNumberAsync(string transactionControlNumber);
}
