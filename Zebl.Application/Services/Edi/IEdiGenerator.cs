using Zebl.Application.Edi.Generation;

namespace Zebl.Application.Services.Edi;

/// <summary>
/// Single entry point for outbound EDI document generation (837 / 270 from claim context).
/// </summary>
public interface IEdiGenerator
{
    Task<string> GenerateAsync(
        Guid receiverLibraryId,
        int claimId,
        OutboundEdiKind kind,
        CancellationToken cancellationToken = default);

    string GenerateEligibility270Async(Eligibility270Envelope envelope);
}
