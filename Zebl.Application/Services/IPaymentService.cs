using Zebl.Application.Dtos.Payments;

namespace Zebl.Application.Services;

/// <summary>
/// Payment entry and disbursement (full payment engine).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Create payment, apply to service lines, create adjustments. Recalculates claim totals.
    /// Throws DuplicatePaymentException if duplicate amount+reference1.
    /// </summary>
    Task<int> CreatePaymentAsync(CreatePaymentCommand command);

    /// <summary>
    /// Auto-apply remaining payment amount to service lines (oldest claim first). Patient or Payer per payment source.
    /// </summary>
    Task AutoApplyPaymentAsync(int paymentId);

    /// <summary>
    /// Disburse remaining amount to given service line applications.
    /// </summary>
    Task DisburseRemainingAsync(int paymentId, List<ServiceLineApplicationDto> applications);

    /// <summary>
    /// Modify payment: reverse existing, create new with command. Returns new payment ID.
    /// </summary>
    Task<int> ModifyPaymentAsync(int paymentId, CreatePaymentCommand command);

    /// <summary>
    /// Delete payment: reverse disbursements, delete adjustments, recalculate service lines and claim totals.
    /// </summary>
    Task RemovePaymentAsync(int paymentId);
}
