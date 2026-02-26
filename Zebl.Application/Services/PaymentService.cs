using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Exceptions;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Full payment engine: create, apply, adjustments, PR unbundling, disbursement, auto-apply, recalc claim totals.
/// Manual and ERA use this same engine. Single transaction; equation check; reconciliation.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IAdjustmentRepository _adjustmentRepo;
    private readonly IServiceLineRepository _serviceLineRepo;
    private readonly IDisbursementRepository _disbursementRepo;
    private readonly IClaimRepository _claimRepo;
    private readonly IPayerRepository _payerRepo;
    private readonly IClaimTotalsService _claimTotalsService;
    private readonly IClaimAuditService _claimAuditService;
    private readonly ITransactionScope _transactionScope;
    private readonly IReconciliationService _reconciliationService;

    private static readonly HashSet<string> ValidGroupCodes = new(StringComparer.OrdinalIgnoreCase) { "CO", "PR", "OA", "PI", "CR" };
    private const decimal Tolerance = 0.01m;

    public PaymentService(
        IPaymentRepository paymentRepo,
        IAdjustmentRepository adjustmentRepo,
        IServiceLineRepository serviceLineRepo,
        IDisbursementRepository disbursementRepo,
        IClaimRepository claimRepo,
        IPayerRepository payerRepo,
        IClaimTotalsService claimTotalsService,
        IClaimAuditService claimAuditService,
        ITransactionScope transactionScope,
        IReconciliationService reconciliationService)
    {
        _paymentRepo = paymentRepo;
        _adjustmentRepo = adjustmentRepo;
        _serviceLineRepo = serviceLineRepo;
        _disbursementRepo = disbursementRepo;
        _claimRepo = claimRepo;
        _payerRepo = payerRepo;
        _claimTotalsService = claimTotalsService;
        _claimAuditService = claimAuditService;
        _transactionScope = transactionScope;
        _reconciliationService = reconciliationService;
    }

    public async Task<int> CreatePaymentAsync(CreatePaymentCommand command)
    {
        ValidateSource(command);
        if (await _paymentRepo.ExistsDuplicateAsync(command.Amount, command.Reference1))
            throw new DuplicatePaymentException($"Duplicate payment: same amount ({command.Amount}) and reference1 ({command.Reference1}).");

        int payerId = command.PaymentSource == PaymentSourceKind.Payer ? (command.PayerId ?? 0) : 0;
        if (command.PaymentSource == PaymentSourceKind.Payer && payerId <= 0)
            throw new InvalidOperationException("PayerId is required when PaymentSource is Payer.");

        bool isPayer = command.PaymentSource == PaymentSourceKind.Payer;
        Payer? payer = payerId > 0 ? await _payerRepo.GetByIdAsync(payerId) : null;
        bool trackPrAdjs = payer?.PayExportTrackedPRAdjs ?? false;

        // Pre-validate: paid + adjustments must not exceed remaining balance (reject overpayment)
        foreach (var app in command.ServiceLineApplications)
        {
            var srv = await _serviceLineRepo.GetTotalsByIdAsync(app.ServiceLineId);
            if (srv == null)
                throw new InvalidOperationException($"Service line {app.ServiceLineId} not found.");
            decimal remaining = GetServiceLineBalance(srv, isPayer);
            decimal adjSum = GetTotalAdjustmentAmountForLine(app, remaining, trackPrAdjs);
            decimal totalApply = app.PaymentAmount + adjSum;
            if (totalApply < -Tolerance)
                throw new InvalidOperationException($"Service line {app.ServiceLineId}: paid + adjustments cannot be negative.");
            if (!command.AllowOverApply && totalApply > remaining + Tolerance)
                throw new InvalidOperationException($"Service line {app.ServiceLineId}: paid amount ({app.PaymentAmount}) + adjustments ({adjSum}) exceeds remaining balance ({remaining}). Overpayment not allowed.");
        }

        await using var transaction = await _transactionScope.BeginTransactionAsync(CancellationToken.None);
        try
        {
            int billingPhysicianId = await ResolveBillingPhysicianAsync(command);
            int paymentId = await _paymentRepo.CreatePaymentAsync(
                command.PaymentSource == PaymentSourceKind.Payer ? command.PayerId : null,
                command.PatientId,
                billingPhysicianId,
                command.Amount,
                command.Date,
                command.Method,
                command.Reference1,
                command.Reference2,
                command.Note,
                command.Ref835);

            decimal totalApplied = 0;
            var affectedClaimIds = new HashSet<int>();

            foreach (var app in command.ServiceLineApplications)
            {
                var srv = await _serviceLineRepo.GetTotalsByIdAsync(app.ServiceLineId);
                if (srv == null) continue;

                decimal balance = GetServiceLineBalance(srv, isPayer);
                decimal toApply = command.AllowOverApply ? app.PaymentAmount : Math.Min(app.PaymentAmount, balance);
                if (toApply <= 0 && !command.AllowOverApply) continue;
                if (toApply <= 0 && command.AllowOverApply && app.PaymentAmount <= 0) continue;

                if (isPayer)
                    await _serviceLineRepo.AddInsPaidAsync(app.ServiceLineId, toApply);
                else
                    await _serviceLineRepo.AddPatPaidAsync(app.ServiceLineId, toApply);
                totalApplied += toApply;
                if (srv.ClaID.HasValue) affectedClaimIds.Add(srv.ClaID.Value);
                Guid srvGuid = await GetServiceLineGuidAsync(app.ServiceLineId);
                await _disbursementRepo.AddAsync(paymentId, app.ServiceLineId, srvGuid, toApply);

                bool prBundleAdded = false; // When PR unbundling bundles, add to service line only once
                foreach (var adj in app.Adjustments)
                {
                    if (string.IsNullOrWhiteSpace(adj.GroupCode))
                        throw new InvalidOperationException("Adjustment GroupCode is required.");
                    string gc = adj.GroupCode.Trim().ToUpperInvariant();
                    if (gc.Length > 2) gc = gc.Substring(0, 2);
                    if (!ValidGroupCodes.Contains(gc))
                        throw new InvalidOperationException($"Invalid adjustment GroupCode: {adj.GroupCode}. Must be CO, PR, OA, PI, or CR.");

                    decimal adjAmount = GetAdjustmentAmountToApply(adj, app, balance, trackPrAdjs, ref prBundleAdded);
                    decimal reasonAmount = adj.ReasonAmount;
                    if (gc == "PR" && trackPrAdjs && app.Adjustments.Count(a => a.GroupCode?.Trim().ToUpperInvariant().StartsWith("PR") == true) > 1)
                    {
                        var prSum = app.Adjustments.Where(a => a.GroupCode?.Trim().ToUpperInvariant().StartsWith("PR") == true).Sum(a => a.ReasonAmount);
                        if (Math.Abs(prSum - balance) > Tolerance)
                            reasonAmount = prSum;
                    }

                    await _adjustmentRepo.AddAsync(paymentId, payerId > 0 ? payerId : await GetPayerIdFromServiceLineAsync(app.ServiceLineId), app.ServiceLineId, srvGuid, gc, adj.ReasonCode, adj.RemarkCode, adjAmount, reasonAmount);
                    await _serviceLineRepo.AddAdjustmentAmountAsync(app.ServiceLineId, gc, adjAmount);
                    if (srv.ClaID.HasValue) affectedClaimIds.Add(srv.ClaID.Value);
                }
            }

            await RecalculateAffectedClaimsAsync(affectedClaimIds);

            // Equation check: Charge = Paid + Adjustments + Balance (per claim)
            foreach (var claimId in affectedClaimIds.Distinct())
            {
                var lines = await _serviceLineRepo.GetTotalsByClaimIdAsync(claimId);
                var totals = _claimTotalsService.RecalculateFromServiceLines(lines);
                decimal totalAdj = totals.TotalCOAdj + totals.TotalCRAdj + totals.TotalOAAdj + totals.TotalPIAdj + totals.TotalPRAdj;
                decimal totalPaid = totals.TotalInsAmtPaid + totals.TotalPatAmtPaid;
                decimal balance = totals.TotalCharge - totalPaid - totalAdj;
                if (Math.Abs(totals.TotalCharge - (totalPaid + totalAdj + balance)) > Tolerance)
                    throw new InvalidOperationException($"Claim {claimId}: equation failed (Charge ≠ Paid + Adjustments + Balance). Rolled back.");
                if (balance < -Tolerance)
                    throw new InvalidOperationException($"Claim {claimId}: balance would be negative ({balance}). Rolled back.");
            }

            await _paymentRepo.SetDisbursedAsync(paymentId, totalApplied);

            // Reconciliation: do not allow silent financial drift
            foreach (var claimId in affectedClaimIds.Distinct())
            {
                var recon = await _reconciliationService.VerifyClaimAsync(claimId, CancellationToken.None);
                if (!recon.Success)
                    throw new InvalidOperationException($"Reconciliation failed for claim {claimId}: {recon.ErrorMessage} {recon.Details}");
            }

            await transaction.CommitAsync(CancellationToken.None);
            return paymentId;
        }
        catch
        {
            // Dispose without commit = rollback
            throw;
        }
    }

    /// <summary>Total adjustment amount that will be applied to the service line (PR bundle = one sum when mismatch).</summary>
    private static decimal GetTotalAdjustmentAmountForLine(ServiceLineApplicationDto app, decimal lineBalance, bool trackPrAdjs)
    {
        var prs = app.Adjustments.Where(a => (a.GroupCode?.Trim().ToUpperInvariant() ?? "").StartsWith("PR")).ToList();
        decimal prTotal = 0;
        if (trackPrAdjs && prs.Count > 1)
        {
            var prSum = prs.Sum(a => a.ReasonAmount);
            prTotal = Math.Abs(prSum - lineBalance) > Tolerance ? prSum : prs.Sum(a => a.Amount);
        }
        else if (prs.Count == 1)
            prTotal = prs[0].Amount;
        else if (prs.Count > 1)
            prTotal = prs.Sum(a => a.Amount);
        decimal other = app.Adjustments.Where(a => !(a.GroupCode?.Trim().ToUpperInvariant() ?? "").StartsWith("PR")).Sum(a => a.Amount);
        return prTotal + other;
    }

    /// <summary>Amount to apply for this adjustment. When PR bundled, only first PR gets the sum; rest get 0 (service balance correct).</summary>
    private static decimal GetAdjustmentAmountToApply(AdjustmentInputDto adj, ServiceLineApplicationDto app, decimal lineBalance, bool trackPrAdjs, ref bool prBundleAdded)
    {
        string gc = adj.GroupCode?.Trim().ToUpperInvariant() ?? "";
        if (gc.Length > 2) gc = gc.Substring(0, 2);
        if (gc != "PR" || !trackPrAdjs) return adj.Amount;
        var prCount = app.Adjustments.Count(a => a.GroupCode?.Trim().ToUpperInvariant().StartsWith("PR") == true);
        if (prCount <= 1) return adj.Amount;
        var prSum = app.Adjustments.Where(a => a.GroupCode?.Trim().ToUpperInvariant().StartsWith("PR") == true).Sum(a => a.ReasonAmount);
        if (Math.Abs(prSum - lineBalance) <= Tolerance) return adj.Amount; // unbundled: use each Amount
        if (prBundleAdded) return 0; // bundled: add to service line only once
        prBundleAdded = true;
        return prSum;
    }

    public async Task AutoApplyPaymentAsync(int paymentId)
    {
        var pay = await _paymentRepo.GetByIdAsync(paymentId);
        if (pay == null) throw new InvalidOperationException("Payment not found.");
        decimal remaining = pay.Value.Amount - pay.Value.Disbursed;
        if (remaining <= 0) return;
        bool isPayer = pay.Value.PayerId.HasValue;
        var lines = await _serviceLineRepo.GetForAutoApplyAsync(pay.Value.PatientId, pay.Value.PayerId, isPayer);
        var affectedClaimIds = new HashSet<int>();
        decimal applied = 0;
        foreach (var srv in lines)
        {
            if (applied >= remaining) break;
            decimal balance = isPayer ? (srv.Charges - srv.TotalInsAmtPaid - srv.TotalCOAdj - srv.TotalCRAdj - srv.TotalOAAdj - srv.TotalPIAdj - srv.TotalPRAdj) : (srv.Charges - srv.TotalPatAmtPaid - srv.TotalCOAdj - srv.TotalCRAdj - srv.TotalOAAdj - srv.TotalPIAdj - srv.TotalPRAdj);
            if (balance <= 0) continue;
            decimal toApply = Math.Min(remaining - applied, balance);
            if (isPayer)
                await _serviceLineRepo.AddInsPaidAsync(srv.SrvID, toApply);
            else
                await _serviceLineRepo.AddPatPaidAsync(srv.SrvID, toApply);
            applied += toApply;
            if (srv.ClaID.HasValue) affectedClaimIds.Add(srv.ClaID.Value);
        }
        await RecalculateAffectedClaimsAsync(affectedClaimIds);
        await _paymentRepo.SetDisbursedAsync(paymentId, pay.Value.Disbursed + applied);
    }

    public async Task DisburseRemainingAsync(int paymentId, List<ServiceLineApplicationDto> applications)
    {
        var pay = await _paymentRepo.GetByIdAsync(paymentId);
        if (pay == null) throw new InvalidOperationException("Payment not found.");
        decimal remaining = pay.Value.Amount - pay.Value.Disbursed;
        if (remaining <= 0) return;
        bool isPayer = pay.Value.PayerId.HasValue;
        var affectedClaimIds = new HashSet<int>();
        decimal totalApply = 0;
        foreach (var app in applications)
        {
            var info = await _serviceLineRepo.GetBalanceInfoAsync(app.ServiceLineId);
            if (info == null) continue;
            decimal balance = isPayer ? (info.Value.Charges - info.Value.InsPaid - info.Value.TotalAdj) : (info.Value.Charges - info.Value.PatPaid - info.Value.TotalAdj);
            if (balance <= 0) continue;
            decimal toApply = Math.Min(app.PaymentAmount, Math.Min(remaining - totalApply, balance));
            if (toApply <= 0) continue;
            if (isPayer)
                await _serviceLineRepo.AddInsPaidAsync(app.ServiceLineId, toApply);
            else
                await _serviceLineRepo.AddPatPaidAsync(app.ServiceLineId, toApply);
            totalApply += toApply;
            var srv = await _serviceLineRepo.GetTotalsByIdAsync(app.ServiceLineId);
            if (srv?.ClaID != null) affectedClaimIds.Add(srv.ClaID.Value);
            Guid srvGuid = await GetServiceLineGuidAsync(app.ServiceLineId);
            await _disbursementRepo.AddAsync(paymentId, app.ServiceLineId, srvGuid, toApply);
        }
        await RecalculateAffectedClaimsAsync(affectedClaimIds);
        await _paymentRepo.SetDisbursedAsync(paymentId, pay.Value.Disbursed + totalApply);
    }

    public async Task<int> ModifyPaymentAsync(int paymentId, CreatePaymentCommand command)
    {
        await RemovePaymentAsync(paymentId);
        return await CreatePaymentAsync(command);
    }

    public async Task RemovePaymentAsync(int paymentId)
    {
        var pay = await _paymentRepo.GetByIdAsync(paymentId);
        if (pay == null) return;
        var adjs = await _adjustmentRepo.GetByPaymentIdAsync(paymentId);
        foreach (var (_, srvId, groupCode, amount) in adjs)
            await _serviceLineRepo.AddAdjustmentAmountAsync(srvId, groupCode, -amount);
        var disbs = await _disbursementRepo.GetByPaymentIdAsync(paymentId);
        foreach (var (_, srvId, amount) in disbs)
        {
            if (pay.Value.PayerId.HasValue)
                await _serviceLineRepo.AddInsPaidAsync(srvId, -amount);
            else
                await _serviceLineRepo.AddPatPaidAsync(srvId, -amount);
        }
        await _adjustmentRepo.DeleteByPaymentIdAsync(paymentId);
        await _disbursementRepo.DeleteByPaymentIdAsync(paymentId);
        var affectedClaimIds = new HashSet<int>();
        foreach (var (_, srvId, _, _) in adjs)
        {
            var srv = await _serviceLineRepo.GetTotalsByIdAsync(srvId);
            if (srv?.ClaID != null) affectedClaimIds.Add(srv.ClaID.Value);
        }
        foreach (var (_, srvId, _) in disbs)
        {
            var srv = await _serviceLineRepo.GetTotalsByIdAsync(srvId);
            if (srv?.ClaID != null) affectedClaimIds.Add(srv.ClaID.Value);
        }
        await RecalculateAffectedClaimsAsync(affectedClaimIds);
        await _paymentRepo.DeleteAsync(paymentId);
    }

    private static void ValidateSource(CreatePaymentCommand command)
    {
        if (command.PaymentSource == PaymentSourceKind.Payer && (!command.PayerId.HasValue || command.PayerId.Value <= 0))
            throw new InvalidOperationException("PayerId is required when PaymentSource is Payer.");
        if (command.PaymentSource == PaymentSourceKind.Patient && command.PatientId <= 0)
            throw new InvalidOperationException("PatientId is required when PaymentSource is Patient.");
    }

    private async Task<int> ResolveBillingPhysicianAsync(CreatePaymentCommand command)
    {
        if (command.BillingPhysicianId.HasValue && command.BillingPhysicianId.Value > 0)
            return command.BillingPhysicianId.Value;
        var first = command.ServiceLineApplications.FirstOrDefault();
        if (first != null)
        {
            var srv = await _serviceLineRepo.GetTotalsByIdAsync(first.ServiceLineId);
            if (srv?.ClaID != null)
            {
                var phyId = await _claimRepo.GetBillingPhysicianIdAsync(srv.ClaID.Value);
                if (phyId.HasValue) return phyId.Value;
            }
        }
        return 0;
    }

    private static decimal GetServiceLineBalance(ServiceLineTotals srv, bool isPayer)
    {
        decimal paid = isPayer ? srv.TotalInsAmtPaid : srv.TotalPatAmtPaid;
        decimal totalAdj = srv.TotalCOAdj + srv.TotalCRAdj + srv.TotalOAAdj + srv.TotalPIAdj + srv.TotalPRAdj;
        return srv.Charges - paid - totalAdj;
    }

    private async Task RecalculateAffectedClaimsAsync(IEnumerable<int> claimIds)
    {
        foreach (var claimId in claimIds.Distinct())
        {
            var serviceLines = await _serviceLineRepo.GetTotalsByClaimIdAsync(claimId);
            var totals = _claimTotalsService.RecalculateFromServiceLines(serviceLines);
            await _claimRepo.UpdateTotalsAsync(claimId, totals);
            await _claimAuditService.RecordClaimEditedAsync(claimId, "Payment Applied", "Claim edited - payment applied.");
        }
    }

    private async Task<Guid> GetServiceLineGuidAsync(int serviceLineId)
    {
        var srv = await _serviceLineRepo.GetTotalsByIdAsync(serviceLineId);
        return srv?.SrvGUID ?? default;
    }

    private async Task<int> GetPayerIdFromServiceLineAsync(int serviceLineId)
    {
        return await _serviceLineRepo.GetPayerIdForLineAsync(serviceLineId);
    }
}
