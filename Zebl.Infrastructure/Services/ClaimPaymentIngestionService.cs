using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Services;

public sealed class ClaimPaymentIngestionService : IClaimPaymentIngestionService
{
    private readonly ZeblDbContext _dbContext;
    private readonly ILogger<ClaimPaymentIngestionService> _logger;

    public ClaimPaymentIngestionService(
        ZeblDbContext dbContext,
        ILogger<ClaimPaymentIngestionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ClaimPaymentIngestionResult> Ingest835Async(Edi835ParseResult parsed, string correlationId, CancellationToken cancellationToken = default)
    {
        var matched = 0;
        var unmatched = 0;
        var duplicates = 0;
        var invalid = 0;
        var trace = string.IsNullOrWhiteSpace(parsed.TraceNumber) ? "NoTrace" : parsed.TraceNumber;

        foreach (var payment in parsed.ClaimPayments)
        {
            var claimExternalId = payment.ClaimId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(claimExternalId) || string.Equals(trace, "NoTrace", StringComparison.Ordinal))
            {
                invalid++;
                _logger.LogWarning(
                    "Invalid 835 payment skipped. CorrelationId={CorrelationId} Trace={Trace} ClaimId={ClaimId} Reason={Reason}",
                    correlationId,
                    trace,
                    claimExternalId,
                    string.IsNullOrWhiteSpace(claimExternalId) ? "MissingClaimId" : "MissingTrace");
                continue;
            }

            var duplicate = await _dbContext.Set<ClaimPayment>()
                .AsNoTracking()
                .AnyAsync(p =>
                    p.TraceNumber == trace
                    && p.ClaimExternalId == claimExternalId
                    && p.PaidAmount == (payment.PaidAmount ?? 0m),
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicate)
            {
                duplicates++;
                _logger.LogInformation("Duplicate 835 claim payment skipped. CorrelationId={CorrelationId} Trace={Trace} ClaimId={ClaimId} PaidAmount={PaidAmount}", correlationId, trace, claimExternalId, payment.PaidAmount);
                continue;
            }

            var externalDupByAmountReference = await _dbContext.Set<ClaimPayment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.TraceNumber == trace
                         && p.PaidAmount == (payment.PaidAmount ?? 0m),
                    cancellationToken)
                .ConfigureAwait(false);
            if (externalDupByAmountReference != null)
            {
                duplicates++;
                _logger.LogWarning(
                    "External duplicate detected (amount+reference). CorrelationId={CorrelationId} Trace={Trace} PaidAmount={PaidAmount} ExistingPaymentId={ExistingPaymentId}",
                    correlationId, trace, payment.PaidAmount, externalDupByAmountReference.Id);
                continue;
            }

            var claim = await ResolveClaimAsync(claimExternalId, cancellationToken).ConfigureAwait(false);
            var claimId = claim?.ClaID;

            var adjustmentAmount = (payment.TotalCharge ?? 0m) - (payment.PaidAmount ?? 0m) - (payment.PatientResponsibility ?? 0m);
            var row = new ClaimPayment
            {
                TenantId = _dbContext.ScopedTenantIdForQuery,
                FacilityId = _dbContext.ScopedFacilityIdForQuery,
                ClaimId = claimId,
                ClaimExternalId = claimExternalId,
                TraceNumber = trace,
                PaidAmount = payment.PaidAmount ?? 0m,
                InsuranceAppliedAmount = 0m,
                TotalCharge = payment.TotalCharge,
                AdjustmentAmount = adjustmentAmount,
                PatientResponsibility = payment.PatientResponsibility,
                PayerId = null,
                PayerLevel = null,
                ChargeAmount = payment.TotalCharge,
                StatusCode = payment.StatusCode,
                ServiceLineCode = string.Empty,
                IsApplied = false,
                CheckDateUtc = DateTime.UtcNow,
                PaymentDateUtc = DateTime.UtcNow,
                IsOrphan = claimId == null,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _dbContext.Set<ClaimPayment>().AddAsync(row, cancellationToken).ConfigureAwait(false);
            if (claimId == null)
            {
                unmatched++;
                _logger.LogWarning("Unmatched claim for 835 payment. CorrelationId={CorrelationId} Trace={Trace} ClaimId={ClaimId}", correlationId, trace, claimExternalId);
            }
            else
            {
                matched++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ClaimPaymentIngestionResult(parsed.ClaimPayments.Count, matched, unmatched, duplicates, invalid);
    }

    private async Task<Zebl.Infrastructure.Persistence.Entities.Claim?> ResolveClaimAsync(string claimExternalId, CancellationToken cancellationToken)
    {
        var normalized = (claimExternalId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ediId))
            return null;

        var canonical = ediId.ToString(CultureInfo.InvariantCulture);
        return await _dbContext.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.ClaEdiClaimId == canonical
                     && c.TenantId == _dbContext.ScopedTenantIdForQuery
                     && c.FacilityId == _dbContext.ScopedFacilityIdForQuery,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

