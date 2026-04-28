using System.Linq;
using Microsoft.Extensions.Logging;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Repositories;

namespace Zebl.Api.Services.Edi;

/// <summary>
/// Claims-domain reaction to inbound 999 files (rejection rows). Keeps core EDI persistence free of claim repositories.
/// </summary>
public sealed class Claim999InboundPostProcessor : IEdiInboundPostProcessor
{
    private readonly IClaimSubmissionRepository _claimSubmissionRepository;
    private readonly IClaimRejectionRepository _claimRejectionRepository;
    private readonly ILogger<Claim999InboundPostProcessor> _logger;

    public Claim999InboundPostProcessor(
        IClaimSubmissionRepository claimSubmissionRepository,
        IClaimRejectionRepository claimRejectionRepository,
        ILogger<Claim999InboundPostProcessor> logger)
    {
        _claimSubmissionRepository = claimSubmissionRepository;
        _claimRejectionRepository = claimRejectionRepository;
        _logger = logger;
    }

    public async Task ProcessInboundPersistedAsync(EdiReport report, ReadOnlyMemory<byte> ediBytes, string fileType, string correlationId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(fileType, "999", StringComparison.OrdinalIgnoreCase))
            return;

        string content;
        try
        {
            content = System.Text.Encoding.UTF8.GetString(ediBytes.Span);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "999 post-process: could not decode EDI bytes for report {ReportId}. CorrelationId={CorrelationId}", report.Id, correlationId);
            throw new InvalidOperationException($"999 EDI bytes are not valid UTF-8 for report {report.Id}.", ex);
        }

        Edi999ParseResult parsed;
        try
        {
            parsed = Edi999Parser.Parse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "999 post-process: parse failed for report {ReportId}. CorrelationId={CorrelationId}", report.Id, correlationId);
            throw new InvalidOperationException($"999 EDI parse failed for report {report.Id}.", ex);
        }

        foreach (var r in parsed.Rejections)
        {
            var submission = await _claimSubmissionRepository.GetByTransactionControlNumberAsync(r.TransactionControlNumber)
                .ConfigureAwait(false);
            var rejection = new ClaimRejection
            {
                ClaimId = submission?.ClaimId,
                EdiReportId = report.Id,
                ErrorCode = submission == null ? "UNMATCHED_999" : r.ErrorCode,
                Description = submission == null
                    ? "999 rejection could not be matched to a claim submission"
                    : r.Description,
                Segment = r.Segment,
                Element = r.Element,
                Status = "New",
                CreatedAt = DateTime.UtcNow,
                TransactionControlNumber = r.TransactionControlNumber
            };

            await _claimRejectionRepository.AddAsync(rejection).ConfigureAwait(false);
        }

        if (parsed.Ik5Lines.Count > 0)
        {
            _logger.LogInformation(
                "999 post-process: IK5 transaction status for report {ReportId}: {Codes}. CorrelationId={CorrelationId}",
                report.Id,
                string.Join(", ", parsed.Ik5Lines.Select(i => i.TransactionSetAcknowledgmentCode ?? "?")),
                correlationId);
        }
    }
}
