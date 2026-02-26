namespace Zebl.Application.Repositories;

/// <summary>
/// Creates adjustment records (e.g. from 835 ERA CO/PR/OA/PI/CR). Implemented in Infrastructure.
/// </summary>
public interface IAdjustmentRepository
{
    /// <summary>
    /// Adds one ERA adjustment to a payment and service line.
    /// </summary>
    Task AddForEraAsync(int paymentId, int payId, int serviceLineId, string groupCode, string? reasonCode, decimal amount);

    /// <summary>
    /// Add adjustment with full fields (payment entry). GroupCode required.
    /// </summary>
    Task AddAsync(int paymentId, int payerId, int serviceLineId, Guid serviceLineGuid, string groupCode, string? reasonCode, string? remarkCode, decimal amount, decimal reasonAmount);

    Task<List<(int AdjId, int SrvId, string GroupCode, decimal Amount)>> GetByPaymentIdAsync(int paymentId);
    Task DeleteByPaymentIdAsync(int paymentId);
}
