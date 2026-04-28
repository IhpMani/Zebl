using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Production 835 auto-post: apply-before-persist, strict dedup/recovery, COB, reversals/corrections, line rollup, audit fields.
/// </summary>
public sealed class EdiAutoPostService : IEdiAutoPostService
{
    private readonly ZeblDbContext _dbContext;
    private readonly ILogger<EdiAutoPostService> _logger;
    private readonly IEdiReportRepository _ediReportRepository;
    private readonly IEdiReportContentReader _contentReader;

    public EdiAutoPostService(
        ZeblDbContext dbContext,
        ILogger<EdiAutoPostService> logger,
        IEdiReportRepository ediReportRepository,
        IEdiReportContentReader contentReader)
    {
        _dbContext = dbContext;
        _logger = logger;
        _ediReportRepository = ediReportRepository;
        _contentReader = contentReader;
    }

    public async Task<EdiAutoPostResult> Apply835Async(Guid reportId, string correlationId, string postedBy, CancellationToken cancellationToken = default)
    {
        EdiAutoPostResult? result = null;
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
            _logger.LogInformation(
                "835 apply started. CorrelationId={CorrelationId} ReportId={ReportId} PostedBy={PostedBy}",
                correlationId, reportId, postedBy);

            var report = await _ediReportRepository.GetByIdAsync(reportId).ConfigureAwait(false);
            if (report == null)
                throw new InvalidOperationException($"EDI report {reportId} was not found.");
            if (!string.Equals(report.FileType, "835", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Report {reportId} is not an 835 file.");
            if (string.Equals(report.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("835 apply skipped: already posted. CorrelationId={CorrelationId} ReportId={ReportId}", correlationId, reportId);
                result = new EdiAutoPostResult(0, 0, 0, 0, 0, 0, 0, 0);
                return;
            }
            if (string.Equals(report.Status, "Processing", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Report {reportId} is already processing.");
            report.Status = "Processing";
            await _ediReportRepository.UpdateAsync(report).ConfigureAwait(false);

            var bytes = await _contentReader.ReadAllBytesAsync(report, cancellationToken).ConfigureAwait(false);
            await using var stream = new MemoryStream(bytes, writable: false);
            var parsed = await Edi835Parser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);

            var noTrace = string.IsNullOrWhiteSpace(parsed.TraceNumber)
                          || string.Equals(parsed.TraceNumber, "NoTrace", StringComparison.Ordinal);
            var noClp = parsed.ClaimGroups.Count == 0;
            if (noTrace || noClp)
            {
                report.Status = "Invalid";
                await _ediReportRepository.UpdateAsync(report).ConfigureAwait(false);
                result = new EdiAutoPostResult(0, 0, 0, 0, 0, 0, 1, 0);
                _logger.LogWarning(
                    "835 apply rejected invalid report. CorrelationId={CorrelationId} ReportId={ReportId} NoTrace={NoTrace} NoClp={NoClp}",
                    correlationId, reportId, noTrace, noClp);
                return;
            }

            var applyRunId = Guid.NewGuid();
            var trace = parsed.TraceNumber;
            var payerId = parsed.OriginatingCompanyIdentifier ?? parsed.PayerName ?? "UnknownPayer";
            var checkDateUtc = parsed.CheckDateUtc ?? report.ReceivedAt ?? DateTime.UtcNow;
            var items = Flatten(parsed, payerId, checkDateUtc);
            var expectedBprTotal = parsed.BprPaymentAmount ?? items.Sum(i => i.PaidAmount);
            if (parsed.BprPaymentAmount == null)
            {
                _logger.LogWarning(
                    "835 apply BPR02 missing; using CLP sum fallback. CorrelationId={CorrelationId} ReportId={ReportId} FallbackAmount={FallbackAmount}",
                    correlationId, reportId, expectedBprTotal);
            }

            var processed = items.Count;
            var applied = 0;
            var skipped = 0;
            var duplicatesSkipped = 0;
            var unmatched = 0;
            var invalid = 0;
            var reversed = 0;
            var creditsCreated = 0;
            var totalInsuranceApplied = 0m;
            var totalCreditsCreated = 0m;
            var totalAdjustmentsApplied = 0m;

            _logger.LogInformation(
                "835 apply parsed. CorrelationId={CorrelationId} ReportId={ReportId} Trace={Trace} ItemCount={ItemCount}",
                correlationId, reportId, trace, items.Count);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var batch = await _dbContext.PaymentBatches
                .FirstOrDefaultAsync(b => b.TraceNumber == trace, cancellationToken)
                .ConfigureAwait(false);
            if (batch == null)
            {
                batch = new PaymentBatch
                {
                    TenantId = _dbContext.ScopedTenantIdForQuery,
                    FacilityId = _dbContext.ScopedFacilityIdForQuery,
                    TraceNumber = trace,
                    TotalAmount = 0m,
                    CheckDateUtc = checkDateUtc,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModifiedAtUtc = DateTime.UtcNow
                };
                await _dbContext.PaymentBatches.AddAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            batch.ModifiedAtUtc = DateTime.UtcNow;
            batch.CheckDateUtc = checkDateUtc;

            // Hard cleanup: old orphan/unapplied rows (legacy broken mapping) must not block fresh apply.
            await _dbContext.ClaimPayments
                .Where(p => p.ClaimId == null && !p.IsApplied)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            var lineCursor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(item.ClaimExternalId))
                    {
                        invalid++;
                        continue;
                    }

                    var ediClaimId = item.ClaimExternalId.Trim();
                    var svcKey = item.ServiceLineCode ?? string.Empty;

                    var existing = await FindClaimPaymentForApplyAsync(
                            item.TraceNumber,
                            ediClaimId,
                            item.PaidAmount,
                            svcKey,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var externalDupByAmountReference = await _dbContext.ClaimPayments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            p => p.TraceNumber == item.TraceNumber
                                 && p.PaidAmount == item.PaidAmount,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (existing is { IsApplied: true })
                    {
                        var remaining = existing.PaidAmount - existing.InsuranceAppliedAmount;
                        if (remaining > 0.01m && existing.ClaimId.HasValue)
                        {
                            var partialClaim = await _dbContext.Claims
                                .FirstOrDefaultAsync(c => c.ClaID == existing.ClaimId.Value, cancellationToken)
                                .ConfigureAwait(false);
                            if (partialClaim != null)
                            {
                                var remItem = item with
                                {
                                    PaidAmount = remaining,
                                    WriteOffAmount = 0m,
                                    PatientResponsibilityAmount = 0m,
                                    TakebackAmount = 0m
                                };
                                var remLedger = await ApplyPaymentToLedgerAsync(partialClaim, remItem, lineCursor, cancellationToken).ConfigureAwait(false);
                                var snapBeforePartial = await CaptureClaimAuditSnapshotAsync(partialClaim.ClaID, cancellationToken).ConfigureAwait(false);
                                _logger.LogInformation("835 apply snapshot before partial reprocess. CorrelationId={CorrelationId} ReportId={ReportId} ClaimId={ClaimId} Snapshot={Snapshot}", correlationId, reportId, partialClaim.ClaID, snapBeforePartial);
                                FinalizeClaimBalances(partialClaim);
                                await ValidateClaimLineConsistencyAsync(partialClaim, correlationId, reportId, cancellationToken).ConfigureAwait(false);
                                await ValidateNoNegativeFinancialsAsync(partialClaim, correlationId, reportId, cancellationToken).ConfigureAwait(false);
                                existing.InsuranceAppliedAmount += remLedger.InsuranceAppliedAmount;
                                existing.PostedAtUtc ??= DateTime.UtcNow;
                                existing.SourceReportId ??= reportId;
                                existing.ApplyRunId ??= applyRunId;
                                totalInsuranceApplied += remLedger.InsuranceAppliedAmount;
                                totalAdjustmentsApplied += remItem.WriteOffAmount;
                                if (remLedger.CreditAmount > 0m)
                                {
                                    await AddCreditBalanceAsync(reportId, trace, partialClaim.ClaID, remLedger.CreditAmount, cancellationToken).ConfigureAwait(false);
                                    totalCreditsCreated += remLedger.CreditAmount;
                                    creditsCreated++;
                                }
                                applied++;
                                continue;
                            }
                        }
                        duplicatesSkipped++;
                        _logger.LogInformation(
                            "835 apply duplicate skipped (already applied). CorrelationId={CorrelationId} ReportId={ReportId} Trace={Trace} EdiClaimId={EdiClaimId} Paid={Paid} Svc={Svc}",
                            correlationId, reportId, item.TraceNumber, ediClaimId, item.PaidAmount, svcKey);
                        continue;
                    }

                    // Existing unapplied rows must never block apply. If unrecoverable, fall through and reprocess as fresh.
                    if (existing is { IsApplied: false })
                    {
                        if (!existing.ClaimId.HasValue)
                        {
                            existing = null;
                        }
                        else
                        {
                            var (recovered, creditAdded) = await TryApplyExistingUnappliedAsync(
                                    existing,
                                    item,
                                    ediClaimId,
                                    svcKey,
                                    batch,
                                    reportId,
                                    applyRunId,
                                    postedBy,
                                    trace,
                                    correlationId,
                                    lineCursor,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            if (creditAdded)
                                creditsCreated++;
                            if (creditAdded)
                                totalCreditsCreated += Math.Max(0m, existing.PaidAmount - existing.InsuranceAppliedAmount);
                            if (recovered)
                            {
                                totalInsuranceApplied += existing.InsuranceAppliedAmount;
                                applied++;
                                continue;
                            }

                            // Recovery failed for historical row: reprocess item as a fresh payment.
                            existing = null;
                        }
                    }

                    if (string.Equals(item.StatusCode, "22", StringComparison.Ordinal))
                    {
                        var prior = await FindPriorPaymentForReversalAsync(item, cancellationToken).ConfigureAwait(false);
                        if (prior == null)
                        {
                            skipped++;
                            _logger.LogWarning(
                                "835 reversal: no prior payment. CorrelationId={CorrelationId} Trace={Trace} EdiClaimId={EdiClaimId}",
                                correlationId, item.TraceNumber, ediClaimId);
                            continue;
                        }

                        await ReversePaymentLedgerAsync(prior, cancellationToken).ConfigureAwait(false);
                        reversed++;
                        batch.TotalAmount -= prior.PaidAmount;
                        continue;
                    }

                    var claim = await ResolveClaimForAutoPostAsync(ediClaimId, cancellationToken).ConfigureAwait(false);
                    if (claim == null)
                    {
                        if (externalDupByAmountReference != null)
                        {
                            duplicatesSkipped++;
                            _logger.LogWarning(
                                "835 external duplicate detected (amount+reference). CorrelationId={CorrelationId} ReportId={ReportId} Trace={Trace} Paid={Paid} ExistingPaymentId={ExistingPaymentId}",
                                correlationId, reportId, item.TraceNumber, item.PaidAmount, externalDupByAmountReference.Id);
                            continue;
                        }

                        var orphanRow = await FindClaimPaymentForApplyAsync(
                                item.TraceNumber,
                                ediClaimId,
                                item.PaidAmount,
                                svcKey,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (orphanRow is { IsApplied: true })
                        {
                            duplicatesSkipped++;
                            continue;
                        }

                        // Unapplied orphan rows from historical runs should never block current apply.
                        if (orphanRow is { IsApplied: false })
                        {
                            if (orphanRow.ClaimId.HasValue)
                            {
                                var (recoveredOrphan, creditOrphan) = await TryApplyExistingUnappliedAsync(
                                        orphanRow,
                                        item,
                                        ediClaimId,
                                        svcKey,
                                        batch,
                                        reportId,
                                        applyRunId,
                                        postedBy,
                                        trace,
                                        correlationId,
                                        lineCursor,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                                if (creditOrphan)
                                    creditsCreated++;
                                if (creditOrphan)
                                    totalCreditsCreated += Math.Max(0m, orphanRow.PaidAmount - orphanRow.InsuranceAppliedAmount);
                                if (recoveredOrphan)
                                {
                                    totalInsuranceApplied += orphanRow.InsuranceAppliedAmount;
                                    totalAdjustmentsApplied += orphanRow.AdjustmentAmount ?? 0m;
                                    applied++;
                                    continue;
                                }
                            }
                        }

                        unmatched++;
                        await _dbContext.ClaimPayments.AddAsync(
                            new ClaimPayment
                            {
                                TenantId = _dbContext.ScopedTenantIdForQuery,
                                FacilityId = _dbContext.ScopedFacilityIdForQuery,
                                ClaimId = null,
                                ClaimExternalId = ediClaimId,
                                TraceNumber = item.TraceNumber,
                                PayerId = item.PayerId,
                                PayerLevel = null,
                                PaidAmount = item.PaidAmount,
                                ChargeAmount = item.ChargeAmount,
                                TotalCharge = item.ChargeAmount,
                                AdjustmentAmount = item.WriteOffAmount,
                                TakebackAmount = item.TakebackAmount,
                                PatientResponsibility = item.PatientResponsibilityAmount,
                                StatusCode = item.StatusCode,
                                ServiceLineCode = svcKey,
                                IsApplied = false,
                                IsOrphan = true,
                                CheckDateUtc = item.CheckDateUtc,
                                PaymentDateUtc = DateTime.UtcNow,
                                PostedBy = postedBy,
                                SourceReportId = reportId,
                                ApplyRunId = applyRunId,
                                PaymentBatchId = batch.Id > 0 ? batch.Id : null,
                                InsuranceAppliedAmount = 0m,
                                CreatedAtUtc = DateTime.UtcNow
                            },
                            cancellationToken).ConfigureAwait(false);
                        _logger.LogWarning(
                            "835 apply orphan recorded (no claim match). CorrelationId={CorrelationId} ReportId={ReportId} Trace={Trace} EdiClaimId={EdiClaimId} TenantId={TenantId} FacilityId={FacilityId}",
                            correlationId, reportId, item.TraceNumber, ediClaimId, _dbContext.ScopedTenantIdForQuery, _dbContext.ScopedFacilityIdForQuery);
                        continue;
                    }

                    var priorForCorrection = await FindPriorPaymentForReversalAsync(item, cancellationToken).ConfigureAwait(false);
                    if (string.Equals(item.StatusCode, "23", StringComparison.Ordinal))
                    {
                        if (priorForCorrection != null)
                        {
                            await ReversePaymentLedgerAsync(priorForCorrection, cancellationToken).ConfigureAwait(false);
                            reversed++;
                            batch.TotalAmount -= priorForCorrection.PaidAmount;
                        }
                    }
                    else if (priorForCorrection != null && priorForCorrection.PaidAmount != item.PaidAmount)
                    {
                        await ReversePaymentLedgerAsync(priorForCorrection, cancellationToken).ConfigureAwait(false);
                        reversed++;
                        batch.TotalAmount -= priorForCorrection.PaidAmount;
                    }

                    var trackedClaim = await _dbContext.Claims
                        .FirstOrDefaultAsync(c => c.ClaID == claim.ClaID, cancellationToken)
                        .ConfigureAwait(false);
                    if (trackedClaim == null)
                    {
                        skipped++;
                        continue;
                    }

                    var payerLevel = await ResolvePayerLevelAsync(trackedClaim, item.PayerId, cancellationToken).ConfigureAwait(false);

                    var snapshotBefore = await CaptureClaimAuditSnapshotAsync(trackedClaim.ClaID, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("835 apply snapshot before apply. CorrelationId={CorrelationId} ReportId={ReportId} ClaimId={ClaimId} Snapshot={Snapshot}",
                        correlationId, reportId, trackedClaim.ClaID, snapshotBefore);
                    var beforeLinePaid = await GetRoundedServiceLinePaidSumAsync(trackedClaim.ClaID, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "835 apply totals before. CorrelationId={CorrelationId} ReportId={ReportId} ClaimId={ClaimId} ClaimInsPaid={ClaimInsPaid} LineSumInsPaid={LineSumInsPaid}",
                        correlationId, reportId, trackedClaim.ClaID, RoundMoney(trackedClaim.ClaTotalInsAmtPaidTRIG), beforeLinePaid);

                    var ledger = await ApplyPaymentToLedgerAsync(
                        trackedClaim,
                        item,
                        lineCursor,
                        cancellationToken).ConfigureAwait(false);
                    if (ledger.CreditAmount > 0m)
                    {
                        await AddCreditBalanceAsync(reportId, trace, trackedClaim.ClaID, ledger.CreditAmount, cancellationToken).ConfigureAwait(false);
                        creditsCreated++;
                        totalCreditsCreated += ledger.CreditAmount;
                    }
                    totalInsuranceApplied += ledger.InsuranceAppliedAmount;
                    totalAdjustmentsApplied += item.WriteOffAmount;

                    FinalizeClaimBalances(trackedClaim);
                    await ValidateClaimLineConsistencyAsync(trackedClaim, correlationId, reportId, cancellationToken).ConfigureAwait(false);
                    await ValidateNoNegativeFinancialsAsync(trackedClaim, correlationId, reportId, cancellationToken).ConfigureAwait(false);
                    trackedClaim.ClaStatus = await DetermineClaimStatusAsync(trackedClaim, item, cancellationToken).ConfigureAwait(false);
                    var afterLinePaid = await GetRoundedServiceLinePaidSumAsync(trackedClaim.ClaID, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "835 apply totals after. CorrelationId={CorrelationId} ReportId={ReportId} ClaimId={ClaimId} ClaimInsPaid={ClaimInsPaid} LineSumInsPaid={LineSumInsPaid}",
                        correlationId, reportId, trackedClaim.ClaID, RoundMoney(trackedClaim.ClaTotalInsAmtPaidTRIG), afterLinePaid);

                    var postedUtc = DateTime.UtcNow;
                    if (externalDupByAmountReference != null)
                    {
                        duplicatesSkipped++;
                        _logger.LogWarning(
                            "835 external duplicate detected before insert (amount+reference). CorrelationId={CorrelationId} ReportId={ReportId} Trace={Trace} Paid={Paid} ExistingPaymentId={ExistingPaymentId}",
                            correlationId, reportId, item.TraceNumber, item.PaidAmount, externalDupByAmountReference.Id);
                        continue;
                    }

                    await _dbContext.ClaimPayments.AddAsync(
                        new ClaimPayment
                        {
                            TenantId = _dbContext.ScopedTenantIdForQuery,
                            FacilityId = _dbContext.ScopedFacilityIdForQuery,
                            ClaimId = trackedClaim.ClaID,
                            ClaimExternalId = ediClaimId,
                            TraceNumber = item.TraceNumber,
                            PayerId = item.PayerId,
                            PayerLevel = payerLevel,
                            PaidAmount = item.PaidAmount,
                            InsuranceAppliedAmount = ledger.InsuranceAppliedAmount,
                            ChargeAmount = item.ChargeAmount,
                            TotalCharge = item.ChargeAmount,
                            AdjustmentAmount = item.WriteOffAmount,
                            TakebackAmount = item.TakebackAmount,
                            PatientResponsibility = item.PatientResponsibilityAmount,
                            StatusCode = item.StatusCode,
                            ServiceLineCode = svcKey,
                            IsApplied = true,
                            CheckDateUtc = item.CheckDateUtc,
                            PaymentDateUtc = postedUtc,
                            PostedAtUtc = postedUtc,
                            PostedBy = postedBy,
                            SourceReportId = reportId,
                            ApplyRunId = applyRunId,
                            PaymentBatchId = batch.Id > 0 ? batch.Id : null,
                            IsOrphan = false,
                            CreatedAtUtc = postedUtc
                        },
                        cancellationToken).ConfigureAwait(false);

                    batch.TotalAmount += item.PaidAmount;
                    applied++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "835 apply item failure. CorrelationId={CorrelationId} ReportId={ReportId} ClaimExternalId={ClaimExternalId}", correlationId, reportId, item.ClaimExternalId);
                    throw;
                }
            }

            // Safety invariant: 835 auto-post must never create claims, only apply to existing claims and/or record ClaimPayment rows.
            var pendingClaimInserts = _dbContext.ChangeTracker.Entries<Claim>()
                .Count(e => e.State == EntityState.Added);
            if (pendingClaimInserts > 0)
            {
                throw new InvalidOperationException($"835 auto-post attempted to insert {pendingClaimInserts} claim record(s), which is not allowed.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (batch.Id > 0)
            {
                await _dbContext.ClaimPayments
                    .Where(p => p.SourceReportId == reportId && p.PaymentBatchId == null && p.TraceNumber == trace)
                    .ExecuteUpdateAsync(
                        u => u.SetProperty(p => p.PaymentBatchId, batch.Id),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var reconciledTotal = totalInsuranceApplied + totalCreditsCreated;
            if (Math.Abs(reconciledTotal - expectedBprTotal) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"835 reconciliation failed. Expected BPR02={expectedBprTotal:F2}, AppliedPlusCredits={reconciledTotal:F2}.");
            }
            _logger.LogInformation(
                "835 reconciliation totals. CorrelationId={CorrelationId} ReportId={ReportId} InsuranceApplied={InsuranceApplied} Credits={Credits} Adjustments={Adjustments} Expected={Expected}",
                correlationId, reportId, totalInsuranceApplied, totalCreditsCreated, totalAdjustmentsApplied, expectedBprTotal);

            report.Status = "Posted";
            await _ediReportRepository.UpdateAsync(report).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "835 apply committed. CorrelationId={CorrelationId} ReportId={ReportId} Processed={Processed} Applied={Applied} DuplicatesSkipped={DuplicatesSkipped} Unmatched={Unmatched} Reversed={Reversed} Invalid={Invalid} Skipped={Skipped} Credits={Credits}",
                correlationId, reportId, processed, applied, duplicatesSkipped, unmatched, reversed, invalid, skipped, creditsCreated);

            result = new EdiAutoPostResult(processed, applied, duplicatesSkipped, unmatched, reversed, creditsCreated, invalid, skipped);
            });
        }
        catch
        {
            var failed = await _ediReportRepository.GetByIdAsync(reportId).ConfigureAwait(false);
            if (failed != null)
            {
                failed.Status = "Failed";
                await _ediReportRepository.UpdateAsync(failed).ConfigureAwait(false);
            }
            throw;
        }

        return result ?? new EdiAutoPostResult(0, 0, 0, 0, 0, 0, 0, 0);
    }

    private async Task<ClaimPayment?> FindPriorPaymentForReversalAsync(AutoPostItem item, CancellationToken cancellationToken)
    {
        var svc = item.ServiceLineCode ?? string.Empty;
        return await _dbContext.ClaimPayments
            .Where(p =>
                p.TraceNumber == item.TraceNumber
                && p.ClaimExternalId == item.ClaimExternalId.Trim()
                && p.ServiceLineCode == svc
                && !p.IsReversed
                && p.IsApplied)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AddCreditBalanceAsync(
        Guid reportId,
        string trace,
        int claimId,
        decimal creditAmount,
        CancellationToken cancellationToken)
    {
        if (creditAmount <= 0m)
            return;
        await _dbContext.ClaimCreditBalances.AddAsync(
            new ClaimCreditBalance
            {
                TenantId = _dbContext.ScopedTenantIdForQuery,
                FacilityId = _dbContext.ScopedFacilityIdForQuery,
                ClaimId = claimId,
                SourceReportId = reportId,
                TraceNumber = trace,
                CreditAmount = creditAmount,
                CreatedAtUtc = DateTime.UtcNow
            },
            cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct LedgerApplyOutcome(decimal CreditAmount, decimal InsuranceAppliedAmount);

    /// <summary>Apply insurance/writeoff/takeback to claim and optional service lines.</summary>
    private async Task<LedgerApplyOutcome> ApplyPaymentToLedgerAsync(
        Claim claim,
        AutoPostItem item,
        Dictionary<string, int> lineCursor,
        CancellationToken cancellationToken)
    {
        var writeOff = item.WriteOffAmount;
        var takeback = item.TakebackAmount;

        var isLineLevel = !string.IsNullOrWhiteSpace(item.ServiceLineCode);
        if (isLineLevel)
        {
            var (creditFromLine, insApplied) = await ApplyServiceLinePaymentAsync(claim, item, lineCursor, cancellationToken).ConfigureAwait(false);
            if (insApplied <= 0m)
                throw new InvalidOperationException($"No deterministic matching service line found for SVC '{item.ServiceLineCode}' on claim {claim.ClaID}.");
            await RollupClaimFromServiceLinesAsync(claim, cancellationToken).ConfigureAwait(false);
            return new LedgerApplyOutcome(creditFromLine, insApplied);
        }

        // CLP-level payment without SVC: allocate sequentially across open service lines (balance-aware).
        var (claimLevelCredit, claimLevelApplied) = await ApplyClaimLevelSequentialAsync(claim, item, cancellationToken).ConfigureAwait(false);
        return new LedgerApplyOutcome(claimLevelCredit, claimLevelApplied);
    }

    private static decimal RemainingClaimBalance(Claim claim)
    {
        return Math.Max(
            0m,
            claim.ClaTotalChargeTRIG - claim.ClaTotalInsAmtPaidTRIG - (claim.ClaTotalAdjCC ?? 0m));
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal ComputeLineBalance(decimal charge, decimal paid, decimal adjustment)
    {
        var balance = RoundMoney(charge - paid - adjustment);
        return Math.Abs(balance) <= 0.01m ? 0m : Math.Max(0m, balance);
    }

    // WARNING:
    // Do NOT use this method inside EF LINQ queries.
    // Use inline expression instead: (s.SrvAllowedAmt > 0m ? s.SrvAllowedAmt : s.SrvCharges)
    private static decimal GetEffectiveLineChargeForMatch(Service_Line line)
    {
        var allowed = line.SrvAllowedAmt;
        return allowed > 0m ? allowed : line.SrvCharges;
    }

    private async Task RollupClaimFromServiceLinesAsync(Claim claim, CancellationToken cancellationToken)
    {
        var lines = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claim.ClaID)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        claim.ClaTotalInsAmtPaidTRIG = RoundMoney(lines.Sum(s => s.SrvTotalInsAmtPaidTRIG));
        claim.ClaTotalAdjCC = RoundMoney(lines.Sum(s => s.SrvTotalAdjCC ?? 0m));
    }

    private async Task<decimal> GetRoundedServiceLinePaidSumAsync(int claimId, CancellationToken cancellationToken)
    {
        var linePaidSum = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claimId)
            .SumAsync(s => (decimal?)s.SrvTotalInsAmtPaidTRIG, cancellationToken)
            .ConfigureAwait(false) ?? 0m;
        return RoundMoney(linePaidSum);
    }

    private static void FinalizeClaimBalances(Claim claim)
    {
        var ins = claim.ClaTotalInsAmtPaidTRIG;
        var wo = claim.ClaTotalAdjCC ?? 0m;
        var pr = Math.Max(0m, claim.ClaTotalChargeTRIG - ins - wo);
        claim.ClaTotalPRAdjTRIG = pr;
        claim.ClaTotalInsBalanceTRIG = pr;
        claim.ClaTotalBalanceCC = pr;
    }

    private async Task<(decimal Credit, decimal InsuranceApplied)> ApplyServiceLinePaymentAsync(
        Claim claim,
        AutoPostItem item,
        Dictionary<string, int> lineCursor,
        CancellationToken cancellationToken)
    {
        var code = ExtractProcedureCode(item.ServiceLineCode);
        if (string.IsNullOrWhiteSpace(code))
            return (0m, 0m);

        var all = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claim.ClaID && s.SrvProcedureCode != null && s.SrvProcedureCode == code)
            .OrderBy(s => s.SrvDateTimeCreated).ThenBy(s => s.SrvID)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (all.Count == 0)
            return (0m, 0m);

        var key = $"{claim.ClaID}|{code}";
        var cursor = lineCursor.TryGetValue(key, out var idx) ? idx : 0;
        var byFull = all
            .Where(s =>
                item.ServiceDate.HasValue
                && s.SrvFromDate == item.ServiceDate.Value
                && Math.Abs(GetEffectiveLineChargeForMatch(s) - (item.ChargeAmount ?? GetEffectiveLineChargeForMatch(s))) <= 0.01m)
            .ToList();
        var byProcedureAndCharge = byFull.Count > 0
            ? byFull
            : all.Where(s => Math.Abs(GetEffectiveLineChargeForMatch(s) - (item.ChargeAmount ?? GetEffectiveLineChargeForMatch(s))) <= 0.01m).ToList();
        var candidates = byProcedureAndCharge.Count > 0 ? byProcedureAndCharge : all;

        // Start at cursor and allocate sequentially across deterministic candidates with open balance.
        var ordered = candidates.Skip(cursor).Concat(candidates.Take(cursor)).ToList();
        var paymentRemaining = item.PaidAmount;
        var totalApplied = 0m;
        var totalOpen = ordered.Sum(s => Math.Max(0m, GetEffectiveLineChargeForMatch(s) - s.SrvTotalInsAmtPaidTRIG - (s.SrvTotalAdjCC ?? 0m)));
        if (totalOpen <= 0m)
            return (item.PaidAmount, 0m);

        foreach (var line in ordered)
        {
            if (paymentRemaining <= 0m)
                break;

            var lineRemaining = Math.Max(0m, GetEffectiveLineChargeForMatch(line) - line.SrvTotalInsAmtPaidTRIG - (line.SrvTotalAdjCC ?? 0m));
            if (lineRemaining <= 0m)
                continue;

            var toApply = Math.Min(paymentRemaining, lineRemaining);
            line.SrvTotalInsAmtPaidTRIG = RoundMoney(line.SrvTotalInsAmtPaidTRIG + toApply);

            decimal adjAdd;
            decimal prAdd;
            decimal takebackPlanned;
            if (ordered.Count == 1)
            {
                adjAdd = item.WriteOffAmount;
                prAdd = item.PatientResponsibilityAmount;
                takebackPlanned = item.TakebackAmount;
            }
            else
            {
                // Distribute CAS components in proportion to applied insurance for this line.
                var ratio = totalOpen <= 0m ? 0m : (toApply / totalOpen);
                adjAdd = item.WriteOffAmount * ratio;
                prAdd = item.PatientResponsibilityAmount * ratio;
                takebackPlanned = item.TakebackAmount * ratio;
            }

            line.SrvTotalAdjCC = RoundMoney((line.SrvTotalAdjCC ?? 0m) + adjAdd);
            line.SrvTotalPRAdjTRIG = RoundMoney(line.SrvTotalPRAdjTRIG + prAdd);
            if (item.TakebackAmount > 0m)
            {
                var safeTakeback = Math.Min(takebackPlanned, line.SrvTotalInsAmtPaidTRIG);
                line.SrvTotalInsAmtPaidTRIG = RoundMoney(Math.Max(0m, line.SrvTotalInsAmtPaidTRIG - safeTakeback));
            }

            var effectiveCharge = GetEffectiveLineChargeForMatch(line);
            line.SrvTotalBalanceCC = ComputeLineBalance(effectiveCharge, line.SrvTotalInsAmtPaidTRIG, line.SrvTotalAdjCC ?? 0m);

            paymentRemaining -= toApply;
            totalApplied += toApply;
        }

        lineCursor[key] = (cursor + 1) % Math.Max(1, candidates.Count);
        return (paymentRemaining, totalApplied);
    }

    /// <summary>
    /// CLP-level (no SVC): apply insurance sequentially across all open lines; carry remainder as claim credit.
    /// </summary>
    private async Task<(decimal Credit, decimal InsuranceApplied)> ApplyClaimLevelSequentialAsync(
        Claim claim,
        AutoPostItem item,
        CancellationToken cancellationToken)
    {
        var lines = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claim.ClaID)
            .OrderBy(s => s.SrvDateTimeCreated).ThenBy(s => s.SrvID)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lines.Count == 0)
        {
            // Claim totals must be derived only from service lines; if none exist, do not mutate claim insurance totals.
            return (item.PaidAmount, 0m);
        }

        var openByLine = lines
            .Select(s => Math.Max(0m, GetEffectiveLineChargeForMatch(s) - s.SrvTotalInsAmtPaidTRIG - (s.SrvTotalAdjCC ?? 0m)))
            .ToList();
        var totalOpen = openByLine.Sum();
        if (totalOpen <= 0m)
            return (item.PaidAmount, 0m);

        var remainingPayment = item.PaidAmount;
        var applied = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            if (remainingPayment <= 0m)
                break;

            var line = lines[i];
            var lineOpen = openByLine[i];
            if (lineOpen <= 0m)
                continue;

            var toApply = Math.Min(remainingPayment, lineOpen);
            line.SrvTotalInsAmtPaidTRIG = RoundMoney(line.SrvTotalInsAmtPaidTRIG + toApply);

            decimal adjAdd;
            decimal prAdd;
            decimal takebackPlanned;
            if (lines.Count == 1)
            {
                adjAdd = item.WriteOffAmount;
                prAdd = item.PatientResponsibilityAmount;
                takebackPlanned = item.TakebackAmount;
            }
            else
            {
                var ratio = totalOpen <= 0m ? 0m : (toApply / totalOpen);
                adjAdd = item.WriteOffAmount * ratio;
                prAdd = item.PatientResponsibilityAmount * ratio;
                takebackPlanned = item.TakebackAmount * ratio;
            }

            line.SrvTotalAdjCC = RoundMoney((line.SrvTotalAdjCC ?? 0m) + adjAdd);
            line.SrvTotalPRAdjTRIG = RoundMoney(line.SrvTotalPRAdjTRIG + prAdd);
            if (item.TakebackAmount > 0m)
            {
                var safeTakeback = Math.Min(takebackPlanned, line.SrvTotalInsAmtPaidTRIG);
                line.SrvTotalInsAmtPaidTRIG = RoundMoney(Math.Max(0m, line.SrvTotalInsAmtPaidTRIG - safeTakeback));
            }
            var effectiveCharge = GetEffectiveLineChargeForMatch(line);
            line.SrvTotalBalanceCC = ComputeLineBalance(effectiveCharge, line.SrvTotalInsAmtPaidTRIG, line.SrvTotalAdjCC ?? 0m);

            remainingPayment -= toApply;
            applied += toApply;
        }

        await RollupClaimFromServiceLinesAsync(claim, cancellationToken).ConfigureAwait(false);
        return (remainingPayment, applied);
    }

    private async Task ReversePaymentLedgerAsync(ClaimPayment payment, CancellationToken cancellationToken)
    {
        if (payment.IsReversed)
            return;

        if (payment.ClaimId.HasValue)
        {
            var claim = await _dbContext.Claims
                .FirstOrDefaultAsync(c => c.ClaID == payment.ClaimId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (claim != null)
            {
                var beforeLinePaid = await GetRoundedServiceLinePaidSumAsync(claim.ClaID, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "835 reversal totals before. ClaimId={ClaimId} ClaimInsPaid={ClaimInsPaid} LineSumInsPaid={LineSumInsPaid}",
                    claim.ClaID, RoundMoney(claim.ClaTotalInsAmtPaidTRIG), beforeLinePaid);

                if (!string.IsNullOrWhiteSpace(payment.ServiceLineCode))
                    await ReverseServiceLineEffectAsync(claim.ClaID, payment, cancellationToken).ConfigureAwait(false);
                else
                {
                    await ReverseClaimLevelEffectAsync(claim.ClaID, payment, cancellationToken).ConfigureAwait(false);
                }

                await RollupClaimFromServiceLinesAsync(claim, cancellationToken).ConfigureAwait(false);
                FinalizeClaimBalances(claim);
                var afterLinePaid = await GetRoundedServiceLinePaidSumAsync(claim.ClaID, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "835 reversal totals after. ClaimId={ClaimId} ClaimInsPaid={ClaimInsPaid} LineSumInsPaid={LineSumInsPaid}",
                    claim.ClaID, RoundMoney(claim.ClaTotalInsAmtPaidTRIG), afterLinePaid);

                var bal = claim.ClaTotalBalanceCC ?? 0m;
                claim.ClaStatus = bal <= 0m ? "Paid" : "Partial";
            }
        }

        payment.IsReversed = true;
        payment.ReversedAtUtc = DateTime.UtcNow;
        payment.IsApplied = false;
    }

    private async Task ReverseServiceLineEffectAsync(int claimId, ClaimPayment payment, CancellationToken cancellationToken)
    {
        var code = ExtractProcedureCode(payment.ServiceLineCode);
        if (string.IsNullOrWhiteSpace(code))
            return;

        var candidates = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claimId && s.SrvProcedureCode == code)
            .OrderByDescending(s => s.SrvID)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paidLeft = payment.InsuranceAppliedAmount > 0m ? payment.InsuranceAppliedAmount : payment.PaidAmount;
        foreach (var line in candidates)
        {
            if (paidLeft <= 0m)
                break;
            if (line.SrvTotalInsAmtPaidTRIG <= 0m)
                continue;
            var take = Math.Min(line.SrvTotalInsAmtPaidTRIG, paidLeft);
            line.SrvTotalInsAmtPaidTRIG -= take;
            paidLeft -= take;
        }

        var adj = payment.AdjustmentAmount ?? 0m;
        var pr = payment.PatientResponsibility ?? 0m;
        var originalTakeback = Math.Max(0m, payment.TakebackAmount ?? 0m);
        var remainingTakeback = originalTakeback;
        foreach (var line in candidates.OrderBy(s => s.SrvID))
        {
            if (adj <= 0m && pr <= 0m && remainingTakeback <= 0m)
                break;
            if (adj > 0m && (line.SrvTotalAdjCC ?? 0m) > 0m)
            {
                var sub = Math.Min(line.SrvTotalAdjCC ?? 0m, adj);
                line.SrvTotalAdjCC = (line.SrvTotalAdjCC ?? 0m) - sub;
                adj -= sub;
            }
            if (pr > 0m && line.SrvTotalPRAdjTRIG > 0m)
            {
                var sub = Math.Min(line.SrvTotalPRAdjTRIG, pr);
                line.SrvTotalPRAdjTRIG -= sub;
                pr -= sub;
            }
            if (remainingTakeback > 0m && line.SrvTotalInsAmtPaidTRIG > 0m)
            {
                var safeTakeback = Math.Min(remainingTakeback, line.SrvTotalInsAmtPaidTRIG);
                line.SrvTotalInsAmtPaidTRIG = Math.Max(0m, line.SrvTotalInsAmtPaidTRIG - safeTakeback);
                remainingTakeback -= safeTakeback;
            }

            line.SrvTotalBalanceCC = Math.Max(
                0m,
                GetEffectiveLineChargeForMatch(line) - line.SrvTotalInsAmtPaidTRIG - (line.SrvTotalAdjCC ?? 0m));
        }

        if (remainingTakeback > 0.01m)
        {
            _logger.LogWarning(
                "835 reversal could not fully apply takeback. ClaimId={ClaimId} PaymentId={PaymentId} RequestedTakeback={RequestedTakeback} AppliedTakeback={AppliedTakeback} RemainingTakeback={RemainingTakeback}",
                claimId,
                payment.Id,
                originalTakeback,
                originalTakeback - remainingTakeback,
                remainingTakeback);
        }
    }

    private async Task ReverseClaimLevelEffectAsync(int claimId, ClaimPayment payment, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claimId)
            .OrderByDescending(s => s.SrvID)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paidLeft = payment.InsuranceAppliedAmount > 0m ? payment.InsuranceAppliedAmount : payment.PaidAmount;
        foreach (var line in candidates)
        {
            if (paidLeft <= 0m)
                break;
            if (line.SrvTotalInsAmtPaidTRIG <= 0m)
                continue;
            var take = Math.Min(line.SrvTotalInsAmtPaidTRIG, paidLeft);
            line.SrvTotalInsAmtPaidTRIG = RoundMoney(Math.Max(0m, line.SrvTotalInsAmtPaidTRIG - take));
            paidLeft -= take;
        }

        var adj = payment.AdjustmentAmount ?? 0m;
        var pr = payment.PatientResponsibility ?? 0m;
        var originalTakeback = Math.Max(0m, payment.TakebackAmount ?? 0m);
        var remainingTakeback = originalTakeback;
        foreach (var line in candidates.OrderBy(s => s.SrvID))
        {
            if (adj <= 0m && pr <= 0m && remainingTakeback <= 0m)
                break;
            if (adj > 0m && (line.SrvTotalAdjCC ?? 0m) > 0m)
            {
                var sub = Math.Min(line.SrvTotalAdjCC ?? 0m, adj);
                line.SrvTotalAdjCC = RoundMoney((line.SrvTotalAdjCC ?? 0m) - sub);
                adj -= sub;
            }
            if (pr > 0m && line.SrvTotalPRAdjTRIG > 0m)
            {
                var sub = Math.Min(line.SrvTotalPRAdjTRIG, pr);
                line.SrvTotalPRAdjTRIG = RoundMoney(line.SrvTotalPRAdjTRIG - sub);
                pr -= sub;
            }
            if (remainingTakeback > 0m && line.SrvTotalInsAmtPaidTRIG > 0m)
            {
                var safeTakeback = Math.Min(remainingTakeback, line.SrvTotalInsAmtPaidTRIG);
                line.SrvTotalInsAmtPaidTRIG = RoundMoney(Math.Max(0m, line.SrvTotalInsAmtPaidTRIG - safeTakeback));
                remainingTakeback -= safeTakeback;
            }

            line.SrvTotalBalanceCC = ComputeLineBalance(
                GetEffectiveLineChargeForMatch(line),
                line.SrvTotalInsAmtPaidTRIG,
                line.SrvTotalAdjCC ?? 0m);
        }

        if (remainingTakeback > 0.01m)
        {
            _logger.LogWarning(
                "835 claim-level reversal could not fully apply takeback. ClaimId={ClaimId} PaymentId={PaymentId} RequestedTakeback={RequestedTakeback} AppliedTakeback={AppliedTakeback} RemainingTakeback={RemainingTakeback}",
                claimId,
                payment.Id,
                originalTakeback,
                originalTakeback - remainingTakeback,
                remainingTakeback);
        }
    }

    private async Task<string> DetermineClaimStatusAsync(Claim claim, AutoPostItem item, CancellationToken cancellationToken)
    {
        if (item.PaidAmount == 0m && item.WriteOffAmount > 0m)
            return "Adjusted";
        if (!string.IsNullOrWhiteSpace(item.ServiceLineCode))
        {
            var open = await _dbContext.Service_Lines
                .AnyAsync(s => s.SrvClaFID == claim.ClaID && (s.SrvTotalBalanceCC ?? 0m) > 0m, cancellationToken)
                .ConfigureAwait(false);
            return open ? "Partial" : "Paid";
        }

        return (claim.ClaTotalBalanceCC ?? 0m) <= 0m ? "Paid" : "Partial";
    }

    private async Task<string> ResolvePayerLevelAsync(Claim claim, string payerId, CancellationToken cancellationToken)
    {
        if (claim.ClaTotalInsAmtPaidTRIG <= 0m)
            return "Primary";
        var insured = await _dbContext.Claim_Insureds
            .AsNoTracking()
            .Where(i => i.ClaInsClaFID == claim.ClaID)
            .OrderBy(i => i.ClaInsSequence ?? int.MaxValue)
            .Select(i => new { i.ClaInsSequence, i.ClaInsPayF.PayExternalID })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var secondary = insured.FirstOrDefault(i => (i.ClaInsSequence ?? 0) == 2)?.PayExternalID;
        if (!string.IsNullOrWhiteSpace(secondary) && string.Equals(secondary, payerId, StringComparison.OrdinalIgnoreCase))
            return "Secondary";
        return "Tertiary";
    }

    private async Task ValidateClaimLineConsistencyAsync(
        Claim claim,
        string correlationId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var linePaidSum = await GetRoundedServiceLinePaidSumAsync(claim.ClaID, cancellationToken).ConfigureAwait(false);
        var claimInsPaid = RoundMoney(claim.ClaTotalInsAmtPaidTRIG);
        if (claimInsPaid != linePaidSum)
        {
            throw new InvalidOperationException(
                $"835 consistency mismatch. ClaimId={claim.ClaID}, ClaimInsPaid={claimInsPaid:F2}, LineInsPaid={linePaidSum:F2}, CorrelationId={correlationId}, ReportId={reportId}");
        }
    }

    private async Task ValidateNoNegativeFinancialsAsync(
        Claim claim,
        string correlationId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        if (claim.ClaTotalInsAmtPaidTRIG < -0.01m || (claim.ClaTotalAdjCC ?? 0m) < -0.01m || (claim.ClaTotalBalanceCC ?? 0m) < -0.01m)
        {
            throw new InvalidOperationException(
                $"835 negative claim totals detected. ClaimId={claim.ClaID}, CorrelationId={correlationId}, ReportId={reportId}");
        }

        var badLine = await _dbContext.Service_Lines
            .Where(s => s.SrvClaFID == claim.ClaID)
            .Where(s =>
                s.SrvTotalInsAmtPaidTRIG < -0.01m
                || (s.SrvTotalAdjCC ?? 0m) < -0.01m
                || (s.SrvTotalBalanceCC ?? 0m) < -0.01m
                || s.SrvTotalInsAmtPaidTRIG > ((s.SrvAllowedAmt > 0m ? s.SrvAllowedAmt : s.SrvCharges) + 0.01m))
            .Select(s => s.SrvID)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (badLine > 0)
        {
            throw new InvalidOperationException(
                $"835 invalid service line totals detected. ClaimId={claim.ClaID}, ServiceLineId={badLine}, CorrelationId={correlationId}, ReportId={reportId}");
        }
    }

    private sealed record ClaimAuditSnapshot(
        int ClaimId,
        decimal ClaimInsPaid,
        decimal ClaimAdj,
        decimal ClaimBalance,
        IReadOnlyList<ServiceLineAuditSnapshot> ServiceLines);

    private sealed record ServiceLineAuditSnapshot(
        int ServiceLineId,
        DateTime CreatedAt,
        decimal InsPaid,
        decimal Adj,
        decimal Balance);

    private async Task<ClaimAuditSnapshot> CaptureClaimAuditSnapshotAsync(int claimId, CancellationToken cancellationToken)
    {
        var claim = await _dbContext.Claims
            .AsNoTracking()
            .Where(c => c.ClaID == claimId)
            .Select(c => new { c.ClaID, c.ClaTotalInsAmtPaidTRIG, c.ClaTotalAdjCC, c.ClaTotalBalanceCC })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var lines = await _dbContext.Service_Lines
            .AsNoTracking()
            .Where(s => s.SrvClaFID == claimId)
            .OrderBy(s => s.SrvDateTimeCreated).ThenBy(s => s.SrvID)
            .Select(s => new ServiceLineAuditSnapshot(
                s.SrvID,
                s.SrvDateTimeCreated,
                s.SrvTotalInsAmtPaidTRIG,
                s.SrvTotalAdjCC ?? 0m,
                s.SrvTotalBalanceCC ?? 0m))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ClaimAuditSnapshot(
            claim.ClaID,
            claim.ClaTotalInsAmtPaidTRIG,
            claim.ClaTotalAdjCC ?? 0m,
            claim.ClaTotalBalanceCC ?? 0m,
            lines);
    }

    /// <summary>
    /// Exact line match first; then match ingest rows that stored an empty service line against apply line items.
    /// </summary>
    private async Task<ClaimPayment?> FindClaimPaymentForApplyAsync(
        string traceNumber,
        string claimExternalId,
        decimal paidAmount,
        string serviceLineCode,
        CancellationToken cancellationToken)
    {
        var svcKey = serviceLineCode ?? string.Empty;
        var exact = await _dbContext.ClaimPayments
            .FirstOrDefaultAsync(
                p => p.TraceNumber == traceNumber
                     && p.PaidAmount == paidAmount
                     && p.ClaimExternalId == claimExternalId
                     && p.ServiceLineCode == svcKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (exact != null)
            return exact;

        if (string.IsNullOrEmpty(svcKey))
            return null;

        return await _dbContext.ClaimPayments
            .FirstOrDefaultAsync(
                p => p.TraceNumber == traceNumber
                     && p.ClaimExternalId == claimExternalId
                     && p.PaidAmount == paidAmount
                     && !p.IsApplied
                     && (p.ServiceLineCode == null || p.ServiceLineCode == string.Empty),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies ledger for a persisted <see cref="ClaimPayment"/> that was never posted. Re-resolves claim when <see cref="ClaimPayment.ClaimId"/> was null.
    /// </summary>
    private async Task<(bool Applied, bool CreditCreated)> TryApplyExistingUnappliedAsync(
        ClaimPayment existing,
        AutoPostItem item,
        string ediClaimId,
        string svcKey,
        PaymentBatch batch,
        Guid reportId,
        Guid applyRunId,
        string postedBy,
        string trace,
        string correlationId,
        Dictionary<string, int> lineCursor,
        CancellationToken cancellationToken)
    {
        Claim? recoverClaim = null;
        if (existing.ClaimId.HasValue)
        {
            recoverClaim = await _dbContext.Claims
                .FirstOrDefaultAsync(c => c.ClaID == existing.ClaimId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            recoverClaim = await ResolveClaimForAutoPostAsync(ediClaimId, cancellationToken).ConfigureAwait(false);
            if (recoverClaim != null)
            {
                existing.ClaimId = recoverClaim.ClaID;
                existing.IsOrphan = false;
            }
        }

        if (recoverClaim == null)
        {
            _logger.LogWarning(
                "835 apply unapplied row still has no claim match. CorrelationId={CorrelationId} PaymentId={PaymentId} EdiClaimId={EdiClaimId}",
                correlationId, existing.Id, ediClaimId);
            return (false, false);
        }

        if (string.IsNullOrEmpty(existing.ServiceLineCode) && !string.IsNullOrEmpty(svcKey))
            existing.ServiceLineCode = svcKey;

        var ledgerRecover = await ApplyPaymentToLedgerAsync(
                recoverClaim,
                item,
                lineCursor,
                cancellationToken)
            .ConfigureAwait(false);
        var creditCreated = false;
        if (ledgerRecover.CreditAmount > 0m)
        {
            await AddCreditBalanceAsync(reportId, trace, recoverClaim.ClaID, ledgerRecover.CreditAmount, cancellationToken).ConfigureAwait(false);
            creditCreated = true;
        }

        FinalizeClaimBalances(recoverClaim);
        await ValidateClaimLineConsistencyAsync(recoverClaim, correlationId, reportId, cancellationToken).ConfigureAwait(false);
        recoverClaim.ClaStatus = await DetermineClaimStatusAsync(recoverClaim, item, cancellationToken).ConfigureAwait(false);

        existing.IsApplied = true;
        existing.IsOrphan = false;
        existing.InsuranceAppliedAmount = ledgerRecover.InsuranceAppliedAmount;
        existing.PostedAtUtc = DateTime.UtcNow;
        existing.PostedBy = postedBy;
        existing.SourceReportId = reportId;
        existing.ApplyRunId = applyRunId;
        existing.PaymentBatchId = batch.Id > 0 ? batch.Id : existing.PaymentBatchId;
        existing.PayerLevel = await ResolvePayerLevelAsync(recoverClaim, item.PayerId, cancellationToken).ConfigureAwait(false);

        batch.TotalAmount += item.PaidAmount;
        _logger.LogInformation(
            "835 apply processed prior unapplied row. CorrelationId={CorrelationId} PaymentId={PaymentId}",
            correlationId, existing.Id);
        return (true, creditCreated);
    }

    private async Task<Claim?> ResolveClaimForAutoPostAsync(string claimExternalId, CancellationToken cancellationToken)
    {
        var normalized = (claimExternalId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ediId))
            return null;

        var canonical = ediId.ToString(CultureInfo.InvariantCulture);
        return await _dbContext.Claims
            .FirstOrDefaultAsync(
                c => c.ClaEdiClaimId == canonical
                     && c.TenantId == _dbContext.ScopedTenantIdForQuery
                     && c.FacilityId == _dbContext.ScopedFacilityIdForQuery,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<AutoPostItem> Flatten(Edi835ParseResult parsed, string payerId, DateTime checkDateUtc)
    {
        var trace = string.IsNullOrWhiteSpace(parsed.TraceNumber) ? "NoTrace" : parsed.TraceNumber;
        var items = new List<AutoPostItem>();
        foreach (var group in parsed.ClaimGroups)
        {
            var claimId = group.ClaimId?.Trim() ?? string.Empty;
            if (group.ServiceLines.Count > 0)
            {
                var lineIndex = 0;
                foreach (var line in group.ServiceLines)
                {
                    var (writeOff, takeback, prLine) = SplitCas(line.Adjustments);
                    var clpPatientPortion = lineIndex == 0 ? (group.PatientResponsibilityAmount ?? 0m) : 0m;
                    items.Add(new AutoPostItem(
                        trace,
                        claimId,
                        payerId,
                        line.LinePaidAmount ?? 0m,
                        line.LineChargeAmount,
                        prLine + clpPatientPortion,
                        writeOff,
                        takeback,
                        group.ClaimStatusCode,
                        line.ProcedureComposite?.Trim() ?? string.Empty,
                        line.ServiceDate,
                        checkDateUtc));
                    lineIndex++;
                }

                continue;
            }

            var (claimWriteOff, claimTakeback, claimPr) = SplitCas(group.Adjustments);
            items.Add(new AutoPostItem(
                trace,
                claimId,
                payerId,
                group.ClaimPaymentAmount ?? 0m,
                group.TotalClaimChargeAmount,
                (group.PatientResponsibilityAmount ?? 0m) + claimPr,
                claimWriteOff,
                claimTakeback,
                group.ClaimStatusCode,
                string.Empty,
                null,
                checkDateUtc));
        }

        return items;
    }

    private static (decimal WriteOff, decimal Takeback, decimal PatientResp) SplitCas(IReadOnlyList<Edi835CasAdjustment> adjustments)
    {
        var writeOff = 0m;
        var takeback = 0m;
        var pr = 0m;
        foreach (var adj in adjustments)
        {
            var amount = adj.Amount ?? 0m;
            if (amount == 0m) continue;
            var gc = (adj.GroupCode ?? string.Empty).Trim().ToUpperInvariant();
            if (gc == "PR")
            {
                pr += amount;
                continue;
            }

            if (gc is "CO" or "OA" or "PI" or "CR")
            {
                if (amount < 0m) takeback += Math.Abs(amount);
                else writeOff += amount;
            }
        }

        return (writeOff, takeback, pr);
    }

    private static string ExtractProcedureCode(string composite)
    {
        if (string.IsNullOrWhiteSpace(composite))
            return string.Empty;
        var n = composite.Trim();
        if (!n.Contains(':', StringComparison.Ordinal))
            return n;
        var parts = n.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
    }

    private sealed record AutoPostItem(
        string TraceNumber,
        string ClaimExternalId,
        string PayerId,
        decimal PaidAmount,
        decimal? ChargeAmount,
        decimal PatientResponsibilityAmount,
        decimal WriteOffAmount,
        decimal TakebackAmount,
        string? StatusCode,
        string ServiceLineCode,
        DateOnly? ServiceDate,
        DateTime CheckDateUtc);
}
