using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Generation;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Services;

public sealed class ClaimEdiDataProvider : IClaimEdiDataProvider
{
    private readonly IClaimExportDataProvider _dataProvider;
    private readonly IClaimRepository _claimRepo;
    private readonly IClaimScrubService _scrubService;
    private readonly ICurrentContext _currentContext;
    private readonly ZeblDbContext _dbContext;

    public ClaimEdiDataProvider(
        IClaimExportDataProvider dataProvider,
        IClaimRepository claimRepo,
        IClaimScrubService scrubService,
        ICurrentContext currentContext,
        ZeblDbContext dbContext)
    {
        _dataProvider = dataProvider;
        _claimRepo = claimRepo;
        _scrubService = scrubService;
        _currentContext = currentContext;
        _dbContext = dbContext;
    }

    public async Task<Claim837EdiContext> Prepare837ContextAsync(int claimId, CancellationToken cancellationToken = default)
    {
        var scrubResults = await _scrubService.ScrubClaimAsync(claimId).ConfigureAwait(false);
        var blocking = scrubResults.Where(r => string.Equals(r.Severity, "Error", StringComparison.OrdinalIgnoreCase)).ToList();
        if (blocking.Count > 0)
        {
            var message = "Claim failed scrubbing:\n" +
                          string.Join("\n", blocking.Select(r => $"- {r.RuleName}: {r.Message} ({r.AffectedField})"));
            throw new InvalidOperationException(message);
        }

        var data = await _dataProvider.GetExportDataAsync(claimId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Claim not found.");

        if (data.Payer == null)
            throw new InvalidOperationException("Payer not found for this claim. Ensure primary insured has a payer.");

        var payer = data.Payer;

        if (!string.Equals(payer.PaySubmissionMethod, "Electronic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This payer is configured for Paper submission.");

        if (string.IsNullOrWhiteSpace(payer.PayExternalID))
            throw new InvalidOperationException("Payer ID is required for electronic submission.");

        var claimFilingIndicator = !string.IsNullOrWhiteSpace(payer.PayClaimFilingIndicator)
            ? payer.PayClaimFilingIndicator
            : data.PrimaryInsured?.ClaInsClaimFilingIndicator;

        var insuranceTypeCode = !string.IsNullOrWhiteSpace(data.ClaInsuranceTypeCodeOverride)
            ? data.ClaInsuranceTypeCodeOverride
            : payer.PayInsTypeCode;

        // Round-trip EDI identifier: same value in 837 CLM01 and Claim.ClaEdiClaimId for 835 CLP01 matching.
        // Preserve existing ClaEdiClaimId (e.g. backfilled from ClaExternalFID or payer echo) — do not replace with ClaID.
        var claim = await _dbContext.Claims
            .FirstOrDefaultAsync(c => c.ClaID == claimId, cancellationToken)
            .ConfigureAwait(false);

        string ediClaimId;
        if (claim != null && !string.IsNullOrWhiteSpace(claim.ClaEdiClaimId))
        {
            ediClaimId = NormalizeClaEdiClaimId(claim.ClaEdiClaimId);
            if (!string.Equals(claim.ClaEdiClaimId, ediClaimId, StringComparison.Ordinal))
            {
                claim.ClaEdiClaimId = ediClaimId;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            ediClaimId = BuildOutboundClaEdiClaimIdFromClaimId(claimId);
            if (claim != null)
            {
                claim.ClaEdiClaimId = ediClaimId;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        data.ClaEdiClaimId = ediClaimId;

        return new Claim837EdiContext
        {
            Data = data,
            Payer = payer,
            ClaimFilingIndicator = claimFilingIndicator ?? "",
            InsuranceTypeCode = insuranceTypeCode
        };
    }

    public async Task<Eligibility270Envelope> Prepare270EnvelopeAsync(
        int claimId,
        ReceiverLibrary receiver,
        string interchangeControlNumber,
        string groupControlNumber,
        string transactionSetControlNumber,
        CancellationToken cancellationToken = default)
    {
        _ = await _claimRepo.GetByIdAsync(claimId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Claim not found.");

        var data = await _dataProvider.GetExportDataAsync(claimId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Claim export data not found.");

        return Eligibility270EnvelopeMappers.FromReceiverAndClaim837Export(
            receiver,
            data,
            interchangeControlNumber,
            groupControlNumber,
            transactionSetControlNumber);
    }

    /// <summary>First-time outbound id when <c>Claim.ClaEdiClaimId</c> is empty: internal claim id.</summary>
    private static string BuildOutboundClaEdiClaimIdFromClaimId(int claimId)
    {
        var s = claimId.ToString(CultureInfo.InvariantCulture).Trim();
        return s.Length > 50 ? s[..50] : s;
    }

    private static string NormalizeClaEdiClaimId(string value)
    {
        var s = (value ?? string.Empty).Trim();
        return s.Length > 50 ? s[..50] : s;
    }
}
