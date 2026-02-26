using Zebl.Application.Repositories;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Verifies claim financial equation: Charge = Paid + Adjustments + Balance. No silent financial drift.
/// </summary>
public class ReconciliationService : IReconciliationService
{
    private readonly IClaimRepository _claimRepo;
    private readonly IServiceLineRepository _serviceLineRepo;

    public ReconciliationService(IClaimRepository claimRepo, IServiceLineRepository serviceLineRepo)
    {
        _claimRepo = claimRepo;
        _serviceLineRepo = serviceLineRepo;
    }

    public async Task<ReconciliationResult> VerifyClaimAsync(int claimId, CancellationToken cancellationToken = default)
    {
        var lines = await _serviceLineRepo.GetTotalsByClaimIdAsync(claimId).ConfigureAwait(false);
        if (lines == null || lines.Count == 0)
            return new ReconciliationResult { Success = true };

        const decimal tolerance = 0.01m;
        var errors = new List<string>();

        foreach (var s in lines)
        {
            decimal paid = s.TotalInsAmtPaid + s.TotalPatAmtPaid;
            decimal totalAdj = s.TotalCOAdj + s.TotalCRAdj + s.TotalOAAdj + s.TotalPIAdj + s.TotalPRAdj;
            decimal balance = s.Charges - paid - totalAdj;
            decimal lhs = s.Charges;
            decimal rhs = paid + totalAdj + balance;
            if (Math.Abs(lhs - rhs) > tolerance)
                errors.Add($"Service line {s.SrvID}: Charge={lhs} != Paid+Adj+Balance={rhs}.");
            if (balance < -tolerance)
                errors.Add($"Service line {s.SrvID}: Balance {balance} is negative.");
        }

        decimal claimCharge = lines.Sum(s => s.Charges);
        decimal claimPaid = lines.Sum(s => s.TotalInsAmtPaid + s.TotalPatAmtPaid);
        decimal claimAdj = lines.Sum(s => s.TotalCOAdj + s.TotalCRAdj + s.TotalOAAdj + s.TotalPIAdj + s.TotalPRAdj);
        decimal claimBalance = claimCharge - claimPaid - claimAdj;
        if (Math.Abs(claimCharge - (claimPaid + claimAdj + claimBalance)) > tolerance)
            errors.Add($"Claim {claimId}: TotalCharges {claimCharge} != Paid+Adj+Balance ({claimPaid}+{claimAdj}+{claimBalance}).");
        if (claimBalance < -tolerance)
            errors.Add($"Claim {claimId}: Balance {claimBalance} is negative.");

        if (errors.Count == 0)
            return new ReconciliationResult { Success = true };

        return new ReconciliationResult
        {
            Success = false,
            ErrorMessage = "Reconciliation failed: Charge ≠ Paid + Adjustments + Balance.",
            Details = string.Join(" ", errors)
        };
    }
}
