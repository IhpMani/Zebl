using Zebl.Application.Domain;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Exceptions;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// 835 ERA auto-post: match payer (Payment Matching Key), forwarding logic, post via payment engine, verify balances match 835.
/// </summary>
public class EraPostingService : IEraPostingService
{
    private readonly IPayerRepository _payerRepo;
    private readonly IClaimRepository _claimRepo;
    private readonly IImportLogRepository _importLog;
    private readonly IPaymentService _paymentService;
    private readonly IServiceLineRepository _serviceLineRepo;
    private readonly ISecondaryTriggerService _secondaryTriggerService;

    private const string StatusForwarded = "Processed as Primary, Forwarded to Additional Payer(s)";
    private const string StatusProcessedPrimary = "Processed as Primary";
    private const decimal Tolerance = 0.001m;

    public EraPostingService(
        IPayerRepository payerRepo,
        IClaimRepository claimRepo,
        IImportLogRepository importLog,
        IPaymentService paymentService,
        IServiceLineRepository serviceLineRepo,
        ISecondaryTriggerService secondaryTriggerService)
    {
        _payerRepo = payerRepo;
        _claimRepo = claimRepo;
        _importLog = importLog;
        _paymentService = paymentService;
        _serviceLineRepo = serviceLineRepo;
        _secondaryTriggerService = secondaryTriggerService;
    }

    public async Task<EraPostingResult> ProcessEraAsync(EraFile era)
    {
        var result = new EraPostingResult { BalancesMatch = true };
        if (era?.Claims == null || era.Claims.Count == 0)
        {
            result.Success = true;
            return result;
        }

        Payer? matchedPayer = await ResolvePayerAsync(era.PayerIdentifier);
        if (matchedPayer == null)
        {
            await _importLog.LogEraImportAsync(era.FileName ?? "ERA", $"ERA: No payer match for identifier '{era.PayerIdentifier}'. Not posting.");
            result.PartiallyProcessed = true;
            result.Errors.Add($"No payer match for identifier '{era.PayerIdentifier}'.");
            result.Success = true;
            return result;
        }

        // Forwarding logic: if PayForwardsClaims, set claim status by ERA status
        foreach (var claim in era.Claims)
        {
            if (!claim.ClaimId.HasValue) continue;
            if (!matchedPayer.PayForwardsClaims) continue;
            var statusText = claim.ClaimStatus ?? era.EraStatus;
            if (string.IsNullOrWhiteSpace(statusText)) continue;
            if (statusText.IndexOf(StatusForwarded, StringComparison.OrdinalIgnoreCase) >= 0)
                await _claimRepo.UpdateClaimStatusAsync(claim.ClaimId.Value, "Submitted");
            else if (statusText.IndexOf(StatusProcessedPrimary, StringComparison.OrdinalIgnoreCase) >= 0)
                await _claimRepo.UpdateClaimStatusAsync(claim.ClaimId.Value, "ReadyToSubmit");
        }

        decimal claimAmount = era.Claims.Count > 0
            ? (era.BprTotalAmount / era.Claims.Count)
            : era.BprTotalAmount;
        var fileName = era.FileName ?? "ERA";

        foreach (var eraClaim in era.Claims)
        {
            try
            {
                var amount = eraClaim.PaymentAmount ?? claimAmount;
                var patientId = eraClaim.PatientId ?? 0;
                var billingPhysicianId = eraClaim.BillingPhysicianId ?? 0;
                if (patientId == 0 || billingPhysicianId == 0)
                {
                    result.Errors.Add($"Claim {eraClaim.ClaimId}: missing PatientId or BillingPhysicianId; skip payment.");
                    result.PartiallyProcessed = true;
                    continue;
                }

                // Capture before-totals for balance verification
                var beforeTotals = new Dictionary<int, ServiceLineTotals>();
                foreach (var line in eraClaim.ServiceLines)
                {
                    if (!line.ServiceLineId.HasValue) continue;
                    var t = await _serviceLineRepo.GetTotalsByIdAsync(line.ServiceLineId.Value);
                    if (t != null) beforeTotals[line.ServiceLineId.Value] = t;
                }

                var command = BuildEraPaymentCommand(era, eraClaim, matchedPayer.PayID, amount, fileName);
                int paymentId;
                try
                {
                    paymentId = await _paymentService.CreatePaymentAsync(command);
                }
                catch (DuplicatePaymentException)
                {
                    result.Errors.Add($"Claim {eraClaim.ClaimId}: duplicate payment (same amount and reference); skip.");
                    result.PartiallyProcessed = true;
                    continue;
                }

                result.PaymentsCreated++;
                result.ClaimsUpdated++;

                // Verify balances match 835: compare deltas (after - before) to expected 835 amounts
                foreach (var line in eraClaim.ServiceLines)
                {
                    if (!line.ServiceLineId.HasValue) continue;
                    var after = await _serviceLineRepo.GetTotalsByIdAsync(line.ServiceLineId.Value);
                    if (after == null) continue;
                    beforeTotals.TryGetValue(line.ServiceLineId.Value, out var before);

                    decimal expectedPaid = line.PaidAmount;
                    decimal beforeIns = before?.TotalInsAmtPaid ?? 0;
                    decimal deltaPaid = after.TotalInsAmtPaid - beforeIns;
                    if (Math.Abs(deltaPaid - expectedPaid) > Tolerance)
                    {
                        result.BalancesMatch = false;
                        result.BalanceVerificationErrors.Add(
                            $"Claim {eraClaim.ClaimId} Srv {line.ServiceLineId}: Ins paid delta {deltaPaid:F2} expected {expectedPaid:F2}.");
                    }

                    foreach (var adj in line.Adjustments)
                    {
                        if (string.IsNullOrWhiteSpace(adj.GroupCode)) continue;
                        var gc = adj.GroupCode.Trim().Length > 2 ? adj.GroupCode.Trim().Substring(0, 2).ToUpperInvariant() : adj.GroupCode.Trim().ToUpperInvariant();
                        decimal expectedAdj = adj.Amount;
                        decimal beforeAdj = GetAdjTotal(before, gc);
                        decimal afterAdj = GetAdjTotal(after, gc);
                        decimal deltaAdj = afterAdj - beforeAdj;
                        if (Math.Abs(deltaAdj - expectedAdj) > Tolerance)
                        {
                            result.BalancesMatch = false;
                            result.BalanceVerificationErrors.Add(
                                $"Claim {eraClaim.ClaimId} Srv {line.ServiceLineId} {gc}: adj delta {deltaAdj:F2} expected {expectedAdj:F2}.");
                        }
                    }
                }

                // PART 10 — Same logic for manual and ERA: evaluate secondary after posting (if reconciliation passed we already verified above).
                if (eraClaim.ClaimId.HasValue && result.BalancesMatch)
                {
                    try
                    {
                        await _secondaryTriggerService.EvaluateAndTriggerAsync(eraClaim.ClaimId.Value);
                    }
                    catch
                    {
                        // Do not fail ERA batch if secondary trigger fails (e.g. no secondary insurance).
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Claim {eraClaim.ClaimId}: {ex.Message}");
                result.PartiallyProcessed = true;
            }
        }

        result.Success = true;
        return result;
    }

    private static CreatePaymentCommand BuildEraPaymentCommand(EraFile era, EraClaim eraClaim, int payerId, decimal amount, string fileName)
    {
        var claimId = eraClaim.ClaimId ?? 0;
        var ref1 = $"ERA|{fileName}|{claimId}";
        var applications = new List<ServiceLineApplicationDto>();
        foreach (var line in eraClaim.ServiceLines)
        {
            if (!line.ServiceLineId.HasValue) continue;
            var adjDtos = line.Adjustments
                .Where(a => !string.IsNullOrWhiteSpace(a.GroupCode))
                .Select(a => new AdjustmentInputDto
                {
                    GroupCode = a.GroupCode,
                    ReasonCode = a.ReasonCode,
                    RemarkCode = null,
                    Amount = a.Amount,
                    ReasonAmount = a.Amount
                })
                .ToList();
            applications.Add(new ServiceLineApplicationDto
            {
                ServiceLineId = line.ServiceLineId.Value,
                PaymentAmount = line.PaidAmount,
                Adjustments = adjDtos
            });
        }

        return new CreatePaymentCommand
        {
            PaymentSource = PaymentSourceKind.Payer,
            PayerId = payerId,
            PatientId = eraClaim.PatientId ?? 0,
            Amount = amount,
            Date = era.CheckDate,
            Method = "ERA",
            Reference1 = ref1,
            Reference2 = null,
            Note = $"ERA: {fileName}",
            BillingPhysicianId = eraClaim.BillingPhysicianId,
            AllowOverApply = true,
            Ref835 = fileName,
            ServiceLineApplications = applications
        };
    }

    private static decimal GetAdjTotal(ServiceLineTotals? s, string groupCode)
    {
        if (s == null) return 0;
        return groupCode switch
        {
            "CO" => s.TotalCOAdj,
            "CR" => s.TotalCRAdj,
            "OA" => s.TotalOAAdj,
            "PI" => s.TotalPIAdj,
            "PR" => s.TotalPRAdj,
            _ => 0
        };
    }

    private async Task<Payer?> ResolvePayerAsync(string? payerIdentifier)
    {
        if (string.IsNullOrWhiteSpace(payerIdentifier)) return null;
        var byExternalId = await _payerRepo.GetByExternalIdAsync(payerIdentifier.Trim());
        if (byExternalId == null || byExternalId.Count == 0) return null;
        if (byExternalId.Count == 1) return byExternalId[0];
        var first = byExternalId[0];
        var key = first.PayPaymentMatchingKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var equivalent = await _payerRepo.GetEquivalentPayersByMatchingKeyAsync(key);
            if (equivalent != null && equivalent.Count > 0)
                return equivalent[0];
        }
        return byExternalId[0];
    }
}
