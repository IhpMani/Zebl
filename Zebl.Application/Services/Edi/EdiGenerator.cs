using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Generation;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services.Edi;

public sealed class EdiGenerator : IEdiGenerator
{
    private readonly IReceiverLibraryRepository _receiverRepo;
    private readonly IClaimEdiDataProvider _claimEdiDataProvider;
    private readonly IControlNumberService _controlNumberService;
    private readonly ICurrentContext _currentContext;
    private readonly IEdiValidationService _ediValidationService;

    public EdiGenerator(
        IReceiverLibraryRepository receiverRepo,
        IClaimEdiDataProvider claimEdiDataProvider,
        IControlNumberService controlNumberService,
        ICurrentContext currentContext,
        IEdiValidationService ediValidationService)
    {
        _receiverRepo = receiverRepo;
        _claimEdiDataProvider = claimEdiDataProvider;
        _controlNumberService = controlNumberService;
        _currentContext = currentContext;
        _ediValidationService = ediValidationService;
    }

    public async Task<string> GenerateAsync(
        Guid receiverLibraryId,
        int claimId,
        OutboundEdiKind kind,
        CancellationToken cancellationToken = default)
    {
        var receiver = await _receiverRepo.GetByIdAsync(receiverLibraryId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Receiver not found.");

        switch (kind)
        {
            case OutboundEdiKind.Claim837:
                if (receiver.ExportFormat != ExportFormat.Ansi837_wTilde)
                    throw new InvalidOperationException("Receiver export format must be ANSI 837 for this operation.");
                var ctx = await _claimEdiDataProvider.Prepare837ContextAsync(claimId, cancellationToken).ConfigureAwait(false);
                var cfg = EdiSubmitterReceiverConfig.FromReceiverLibrary(receiver);
                var ctrl = await NextControlNumbersAsync(cancellationToken).ConfigureAwait(false);
                var edi837 = Claim837Builder.BuildInterchange(ctx, cfg, ctrl);
                _ediValidationService.Validate(edi837, OutboundEdiKind.Claim837);
                return edi837;

            case OutboundEdiKind.Eligibility270:
                if (receiver.ExportFormat != ExportFormat.Eligibility270)
                    throw new InvalidOperationException("Receiver export format must be Eligibility 270 for this operation.");
                var icn = await _controlNumberService.GetNextInterchangeControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false);
                var gcn = await _controlNumberService.GetNextGroupControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false);
                var st = await _controlNumberService.GetNextTransactionControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false);
                var env = await _claimEdiDataProvider.Prepare270EnvelopeAsync(claimId, receiver, icn, gcn, st, cancellationToken).ConfigureAwait(false);
                var edi270 = Eligibility270Builder.BuildInterchange(env);
                _ediValidationService.Validate(edi270, OutboundEdiKind.Eligibility270);
                return edi270;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported EDI kind.");
        }
    }

    public string GenerateEligibility270Async(Eligibility270Envelope envelope)
    {
        var edi = Eligibility270Builder.BuildInterchange(envelope);
        _ediValidationService.Validate(edi, OutboundEdiKind.Eligibility270);
        return edi;
    }

    private async Task<EdiControlNumbers> NextControlNumbersAsync(CancellationToken cancellationToken)
    {
        return new EdiControlNumbers
        {
            InterchangeControlNumber = await _controlNumberService.GetNextInterchangeControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false),
            GroupControlNumber = await _controlNumberService.GetNextGroupControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false),
            TransactionControlNumber = await _controlNumberService.GetNextTransactionControlNumber(_currentContext.TenantId, _currentContext.FacilityId).ConfigureAwait(false)
        };
    }
}
