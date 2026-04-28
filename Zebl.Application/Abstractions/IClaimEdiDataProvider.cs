using Zebl.Application.Domain;
using Zebl.Application.Edi.Generation;

namespace Zebl.Application.Abstractions;

/// <summary>
/// Claim-side data for EDI generation; implemented outside the EDI transport/parsing layer.
/// </summary>
public interface IClaimEdiDataProvider
{
    /// <summary>
    /// Validates scrub rules and payer configuration, then returns structured 837 inputs.
    /// </summary>
    Task<Claim837EdiContext> Prepare837ContextAsync(int claimId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads claim export data and builds a 270 envelope for the given receiver and control numbers.
    /// </summary>
    Task<Eligibility270Envelope> Prepare270EnvelopeAsync(
        int claimId,
        ReceiverLibrary receiver,
        string interchangeControlNumber,
        string groupControlNumber,
        string transactionSetControlNumber,
        CancellationToken cancellationToken = default);
}
