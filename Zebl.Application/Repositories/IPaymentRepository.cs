namespace Zebl.Application.Repositories;

/// <summary>
/// Repository for Payment. Application layer abstraction; implemented in Infrastructure.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Creates a payment record (e.g. from 835 ERA). Returns the new PmtID.
    /// </summary>
    Task<int> AddAsync(int payId, int patientId, int billingPhysicianId, decimal amount, DateOnly paymentDate, string? ref835 = null);
}
