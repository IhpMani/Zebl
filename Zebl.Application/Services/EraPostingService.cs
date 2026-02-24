using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// 835 ERA auto-post: match payer (Payment Matching Key), forwarding logic, create payments/adjustments. Does not crash on payer match failure.
/// </summary>
public class EraPostingService : IEraPostingService
{
    private readonly IPayerRepository _payerRepo;
    private readonly IClaimRepository _claimRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IAdjustmentRepository _adjustmentRepo;
    private readonly IImportLogRepository _importLog;

    private const string StatusForwarded = "Processed as Primary, Forwarded to Additional Payer(s)";
    private const string StatusProcessedPrimary = "Processed as Primary";

    public EraPostingService(
        IPayerRepository payerRepo,
        IClaimRepository claimRepo,
        IPaymentRepository paymentRepo,
        IAdjustmentRepository adjustmentRepo,
        IImportLogRepository importLog)
    {
        _payerRepo = payerRepo;
        _claimRepo = claimRepo;
        _paymentRepo = paymentRepo;
        _adjustmentRepo = adjustmentRepo;
        _importLog = importLog;
    }

    public async Task<EraPostingResult> ProcessEraAsync(EraFile era)
    {
        var result = new EraPostingResult();
        if (era?.Claims == null || era.Claims.Count == 0)
        {
            result.Success = true;
            return result;
        }

        // 1) Match payer: PayExternalID; if multiple, treat as same logical payer via Payment Matching Key
        Payer? matchedPayer = await ResolvePayerAsync(era.PayerIdentifier);
        if (matchedPayer == null)
        {
            await _importLog.LogEraImportAsync(era.FileName ?? "ERA", $"ERA: No payer match for identifier '{era.PayerIdentifier}'. Not posting.");
            result.PartiallyProcessed = true;
            result.Errors.Add($"No payer match for identifier '{era.PayerIdentifier}'.");
            result.Success = true; // do not crash batch
            return result;
        }

        // 2) Forwarding logic: if PayForwardsClaims, set claim status by ERA status
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

        // 3) Auto-post payments (inactive payer still allowed for historical consistency)
        decimal claimAmount = era.Claims.Count > 0
            ? (era.BprTotalAmount / era.Claims.Count)
            : era.BprTotalAmount;
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
                var pmtId = await _paymentRepo.AddAsync(
                    matchedPayer.PayID,
                    patientId,
                    billingPhysicianId,
                    amount,
                    era.CheckDate,
                    era.FileName);
                result.PaymentsCreated++;

                // Apply adjustments (CO, PR, OA, PI, CR) to service lines
                foreach (var line in eraClaim.ServiceLines)
                {
                    if (!line.ServiceLineId.HasValue) continue;
                    foreach (var adj in line.Adjustments)
                    {
                        if (string.IsNullOrWhiteSpace(adj.GroupCode)) continue;
                        await _adjustmentRepo.AddForEraAsync(
                            pmtId,
                            matchedPayer.PayID,
                            line.ServiceLineId.Value,
                            adj.GroupCode,
                            adj.ReasonCode,
                            adj.Amount);
                    }
                }
                result.ClaimsUpdated++;
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

    /// <summary>
    /// Match by PayExternalID; if multiple payers, resolve via Payment Matching Key (same logical payer).
    /// </summary>
    private async Task<Payer?> ResolvePayerAsync(string? payerIdentifier)
    {
        if (string.IsNullOrWhiteSpace(payerIdentifier)) return null;
        var byExternalId = await _payerRepo.GetByExternalIdAsync(payerIdentifier.Trim());
        if (byExternalId == null || byExternalId.Count == 0) return null;
        if (byExternalId.Count == 1) return byExternalId[0];
        // Multiple: group by PayPaymentMatchingKey; treat as same logical payer, pick first
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
