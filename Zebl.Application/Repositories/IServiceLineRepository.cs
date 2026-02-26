using Zebl.Application.Domain;
using Zebl.Application.Dtos.Payments;

namespace Zebl.Application.Repositories;

/// <summary>
/// Service line read/update for payment engine. Implemented in Infrastructure.
/// </summary>
public interface IServiceLineRepository
{
    Task<ServiceLineTotals?> GetTotalsByIdAsync(int serviceLineId);
    Task<List<ServiceLineTotals>> GetTotalsByClaimIdAsync(int claimId);
    /// <summary>Service lines for auto-apply: same patient, optional payer, ordered by claim date (oldest first).</summary>
    Task<List<ServiceLineTotals>> GetForAutoApplyAsync(int patientId, int? payerId, bool isPayerSource);
    /// <summary>Service lines for payment entry grid: patient name, DOS, proc, charge, responsible, applied, balance.</summary>
    Task<List<PaymentEntryServiceLineDto>> GetPaymentEntryLinesAsync(int patientId, int? payerId, bool isPayerSource);
    /// <summary>Add amount to SrvTotalInsAmtPaidTRIG. Returns claim id for recalc.</summary>
    Task<int?> AddInsPaidAsync(int serviceLineId, decimal amount);
    /// <summary>Add amount to SrvTotalPatAmtPaidTRIG. Returns claim id for recalc.</summary>
    Task<int?> AddPatPaidAsync(int serviceLineId, decimal amount);
    /// <summary>Add to the appropriate SrvTotal*AdjTRIG by group code (CO, PR, OA, PI, CR). Returns claim id.</summary>
    Task<int?> AddAdjustmentAmountAsync(int serviceLineId, string groupCode, decimal amount);
    /// <summary>Get SrvCharges, SrvAllowedAmt for pay-button logic.</summary>
    Task<(decimal Charges, decimal AllowedAmt, decimal InsPaid, decimal PatPaid, decimal TotalAdj)?> GetBalanceInfoAsync(int serviceLineId);
    /// <summary>Get responsible payer ID for service line (for adjustment).</summary>
    Task<int> GetPayerIdForLineAsync(int serviceLineId);
}
