using Zebl.Application.Edi.Parsing;

namespace Zebl.Application.Services;

public interface IClaimPaymentIngestionService
{
    Task<ClaimPaymentIngestionResult> Ingest835Async(Edi835ParseResult parsed, string correlationId, CancellationToken cancellationToken = default);
}

public sealed record ClaimPaymentIngestionResult(int Total, int Matched, int Unmatched, int Duplicates, int Invalid);

