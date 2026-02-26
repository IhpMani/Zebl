using Zebl.Application.Dtos.Payments;

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

    /// <summary>
    /// Create payment with full fields (payment entry). Returns PmtID. ref835 stored in Pmt835Ref when provided.
    /// </summary>
    Task<int> CreatePaymentAsync(int? payerId, int patientId, int billingPhysicianId, decimal amount, DateOnly date, string? method, string? reference1, string? reference2, string? note, string? ref835);

    Task<(int? PayerId, int PatientId, decimal Amount, decimal Disbursed)?> GetByIdAsync(int paymentId);
    Task<bool> ExistsDuplicateAsync(decimal amount, string? reference1);
    Task SetDisbursedAsync(int paymentId, decimal disbursedAmount);
    Task DeleteAsync(int paymentId);

    /// <summary>Get payment by ID for edit form. Returns null if not found.</summary>
    Task<PaymentForEditDto?> GetPaymentForEditAsync(int paymentId);

    /// <summary>Get payments for claim (by claim's patient). Returns (payments, claimFound). When claim not found, claimFound is false and list is empty.</summary>
    Task<(List<PaymentDto> Payments, bool ClaimFound)> GetPaymentsForClaimAsync(int claimId);

    /// <summary>Get payment list with paging and optional patient filter. Returns data and total count.</summary>
    Task<(List<PaymentListItemDto> Data, int TotalCount)> GetPaymentListAsync(int page, int pageSize, int? patientId);
}
