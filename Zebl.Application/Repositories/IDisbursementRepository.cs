namespace Zebl.Application.Repositories;

/// <summary>
/// Disbursement persistence for payment engine.
/// </summary>
public interface IDisbursementRepository
{
    Task AddAsync(int paymentId, int serviceLineId, Guid serviceLineGuid, decimal amount, string? note = null);
    Task<List<(int DisbId, int SrvId, decimal Amount)>> GetByPaymentIdAsync(int paymentId);
    Task DeleteByPaymentIdAsync(int paymentId);
}
