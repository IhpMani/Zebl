using System.Net.Sockets;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Generation;
using Zebl.Application.Services.Edi;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Services;

public class EligibilityService : IEligibilityService
{
    private const int DefaultSftpPort = 22;

    private readonly ZeblDbContext _db;
    private readonly ICurrentContext _currentContext;
    private readonly IEligibilitySettingsProvider _settingsProvider;
    private readonly IEligibilityParser _eligibilityParser;
    private readonly IEdiGenerator _ediGenerator;
    private readonly EdiReportService _ediReportService;
    private readonly SftpTransportService _sftpTransportService;
    private readonly ILogger<EligibilityService> _logger;

    public EligibilityService(
        ZeblDbContext db,
        ICurrentContext currentContext,
        IEligibilitySettingsProvider settingsProvider,
        IEligibilityParser eligibilityParser,
        IEdiGenerator ediGenerator,
        EdiReportService ediReportService,
        SftpTransportService sftpTransportService,
        ILogger<EligibilityService> logger)
    {
        _db = db;
        _currentContext = currentContext;
        _settingsProvider = settingsProvider;
        _eligibilityParser = eligibilityParser;
        _ediGenerator = ediGenerator;
        _ediReportService = ediReportService;
        _sftpTransportService = sftpTransportService;
        _logger = logger;
    }

    public async Task<EligibilityRequestResultDto> RequestEligibilityAsync(EligibilityRequestCreateDto request, CancellationToken cancellationToken = default)
    {
        var context = await LoadEligibilityContextAsync(request.PatientId, cancellationToken);
        var (runtimeHost, runtimePort) = ParseServer(context.Settings.Server);
        var providerModeDisplay = NormalizeProviderModeDisplay(context.Settings.ProviderMode);
        var controlNumber = BuildEligibilityControlNumber();
        var eligibilityRequest = new EligibilityRequest
        {
            TenantId = _currentContext.TenantId,
            FacilityId = _currentContext.FacilityId,
            PatientId = context.Patient.PatID,
            PayerId = context.Payer.PayID,
            SubscriberId = context.Insured.MemberId!.Trim(),
            ControlNumber = controlNumber,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending",
            ProviderNpi = context.Provider.PhyNPI,
            ProviderMode = providerModeDisplay,
            UsedPayerOverride = context.UsedPayerOverride
        };

        _db.EligibilityRequests.Add(eligibilityRequest);
        await _db.SaveChangesAsync(cancellationToken);

        var fileName = $"eligibility-{eligibilityRequest.Id}.270";
        eligibilityRequest.BatchFileName = fileName;
        var edi270 = _ediGenerator.GenerateEligibility270Async(MapEligibility270Envelope(eligibilityRequest, context));
        var correlationId = Guid.NewGuid().ToString("N");
        var fileBytes = Encoding.UTF8.GetBytes(edi270);
        var reportCreate = await _ediReportService.CreateGeneratedAsync(
            context.Receiver.Id,
            null,
            fileName,
            "270",
            fileBytes,
            correlationId,
            cancellationToken: cancellationToken);
        try
        {
            await using var stream = new MemoryStream(fileBytes, writable: false);
            await _sftpTransportService.UploadFileAsync(
                runtimeHost,
                runtimePort,
                context.Settings.Username,
                context.Settings.Password,
                uploadDirectory: "/outgoing/eligibility",
                fileName,
                stream,
                cancellationToken).ConfigureAwait(false);
            await _ediReportService.MarkSentAsync(reportCreate.Report.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _ediReportService.MarkFailedAsync(reportCreate.Report.Id).ConfigureAwait(false);
            eligibilityRequest.Status = "Failed";
            await _db.SaveChangesAsync(cancellationToken);
            LogEligibilityOutcome(
                request.PatientId,
                context,
                runtimeHost,
                runtimePort,
                outcome: "Failure",
                correlationId: correlationId,
                reason: ex.Message);
            throw new InvalidOperationException(ex.Message);
        }

        eligibilityRequest.Status = "Sent";
        await _db.SaveChangesAsync(cancellationToken);

        LogEligibilityOutcome(
            request.PatientId,
            context,
            runtimeHost,
            runtimePort,
            outcome: "Success",
            correlationId: correlationId,
            reason: null);

        return new EligibilityRequestResultDto
        {
            Id = eligibilityRequest.Id,
            Status = eligibilityRequest.Status,
            BatchFileName = eligibilityRequest.BatchFileName,
            ControlNumber = eligibilityRequest.ControlNumber,
            ProviderNpi = eligibilityRequest.ProviderNpi,
            ProviderMode = eligibilityRequest.ProviderMode,
            UsedPayerOverride = eligibilityRequest.UsedPayerOverride
        };
    }

    public async Task<EligibilityRequestStatusDto?> GetEligibilityStatusAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var request = await _db.EligibilityRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.TenantId == _currentContext.TenantId &&
                r.FacilityId == _currentContext.FacilityId, cancellationToken);
        if (request == null)
            return null;

        var response = await _db.EligibilityResponses
            .AsNoTracking()
            .Where(r => r.RequestId == request.Id)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var parsed = response == null || string.IsNullOrWhiteSpace(response.Raw271)
            ? null
            : _eligibilityParser.Parse(response.Raw271);

        return new EligibilityRequestStatusDto
        {
            Id = request.Id,
            PatientId = request.PatientId,
            PayerId = request.PayerId,
            SubscriberId = request.SubscriberId,
            ControlNumber = request.ControlNumber,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            BatchFileName = request.BatchFileName,
            EligibilityStatus = response?.EligibilityStatus,
            ErrorMessage = response?.ErrorMessage,
            Raw271 = response?.Raw271,
            PayerName = parsed?.PayerName,
            PlanName = parsed?.PlanName,
            PlanDetails = parsed?.PlanDetails,
            EligibilityStartDate = parsed?.EligibilityStartDate,
            EligibilityEndDate = parsed?.EligibilityEndDate,
            Benefits = parsed?.Benefits.Select(b => new EligibilityBenefitDto
            {
                ServiceType = b.ServiceType,
                Benefit = b.Benefit,
                Amount = b.Amount,
                Description = b.Description
            }).ToList() ?? [],
            ProviderNpi = request.ProviderNpi,
            ProviderMode = request.ProviderMode,
            UsedPayerOverride = request.UsedPayerOverride
        };
    }

    public async Task<EligibilityPreflightResultDto> PreflightAsync(EligibilityPreflightRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new EligibilityPreflightResultDto();
        var settings = await _settingsProvider.GetForEligibilityCheckAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.Server))
            result.Errors.Add("Program Setup Patient Eligibility server is required.");
        if (string.IsNullOrWhiteSpace(settings.Username))
            result.Errors.Add("Program Setup Patient Eligibility username is required.");
        if (string.IsNullOrWhiteSpace(settings.Password))
            result.Errors.Add("Program Setup Patient Eligibility password is required.");
        if (!Guid.TryParse(settings.ReceiverId, out var receiverId))
            result.Errors.Add("Eligibility receiver configuration is invalid.");

        if (result.Errors.Count > 0)
            return result;

        var receiver = await _db.ReceiverLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiverId, cancellationToken);
        if (receiver == null)
        {
            result.Errors.Add("Submitter/receiver configuration not found.");
            return result;
        }

        if (!receiver.IsActive)
            result.Errors.Add("Selected receiver library entry is inactive.");
        if (receiver.ExportFormat != ExportFormat.Eligibility270)
            result.Errors.Add("Selected receiver must use export format Eligibility 270.");
        var scopedMismatch =
            (receiver.TenantId.HasValue && receiver.TenantId.Value != _currentContext.TenantId) ||
            (receiver.FacilityId.HasValue && receiver.FacilityId.Value != _currentContext.FacilityId);
        if (scopedMismatch)
            result.Errors.Add("Submitter/receiver configuration is outside current tenant/facility scope.");

        if (result.Errors.Count == 0)
        {
            try
            {
                var (host, port) = ParseServer(settings.Server);
                result.ServerReachable = await TryTcpReachableAsync(host, port, cancellationToken);
                if (result.ServerReachable != true)
                    result.Errors.Add($"Cannot reach eligibility server at {host}:{port} (TCP check failed).");
            }
            catch (Exception ex)
            {
                result.ServerReachable = false;
                result.Errors.Add($"Eligibility server address is invalid: {ex.Message}");
            }
        }

        if (request.PatientId.HasValue && request.PatientId.Value > 0 && result.Errors.Count == 0)
        {
            try
            {
                await LoadEligibilityContextAsync(request.PatientId.Value, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                result.Errors.Add(ex.Message);
            }
        }

        result.Valid = result.Errors.Count == 0;
        return result;
    }

    public async Task<string> Generate270Async(EligibilityRequestCreateDto request, CancellationToken cancellationToken = default)
    {
        var context = await LoadEligibilityContextAsync(request.PatientId, cancellationToken);
        var draftRequest = new EligibilityRequest
        {
            Id = 0,
            TenantId = _currentContext.TenantId,
            FacilityId = _currentContext.FacilityId,
            PatientId = context.Patient.PatID,
            PayerId = context.Payer.PayID,
            SubscriberId = context.Insured.MemberId!.Trim(),
            ControlNumber = BuildEligibilityControlNumber(),
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        return _ediGenerator.GenerateEligibility270Async(MapEligibility270Envelope(draftRequest, context));
    }

    private async Task<EligibilityBuildContext> LoadEligibilityContextAsync(int patientId, CancellationToken cancellationToken)
    {
        var settings = await _settingsProvider.GetForEligibilityCheckAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.Server))
            throw new InvalidOperationException("Program Setup Patient Eligibility server is required.");
        if (string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Program Setup Patient Eligibility username is required.");
        if (string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Program Setup Patient Eligibility password is required.");

        if (!Guid.TryParse(settings.ReceiverId, out var receiverId))
            throw new InvalidOperationException("Eligibility receiver configuration is invalid.");

        var receiver = await _db.ReceiverLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiverId, cancellationToken)
            ?? throw new InvalidOperationException("Submitter/receiver configuration not found.");
        if (!receiver.IsActive)
            throw new InvalidOperationException("Selected receiver library entry is inactive.");
        if (receiver.ExportFormat != ExportFormat.Eligibility270)
            throw new InvalidOperationException("Selected receiver must use export format Eligibility 270.");

        var scopedMismatch =
            (receiver.TenantId.HasValue && receiver.TenantId.Value != _currentContext.TenantId) ||
            (receiver.FacilityId.HasValue && receiver.FacilityId.Value != _currentContext.FacilityId);
        if (scopedMismatch)
            throw new InvalidOperationException("Submitter/receiver configuration is outside current tenant/facility scope.");

        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatID == patientId, cancellationToken)
            ?? throw new InvalidOperationException("Patient not found.");

        var insuranceData = await LoadPrimaryInsuranceAsync(patientId, cancellationToken)
            ?? throw new InvalidOperationException("Primary insurance with payer is required.");
        if (string.IsNullOrWhiteSpace(insuranceData.Subscriber.MemberId))
            throw new InvalidOperationException("Subscriber ID is required.");
        if (!patient.PatBirthDate.HasValue)
            throw new InvalidOperationException("Patient DOB is required.");
        if (string.IsNullOrWhiteSpace(insuranceData.Payer.PayEligibilityPayerID))
            throw new InvalidOperationException("Payer with eligibility external ID is required.");

        var (provider, usedPayerOverride) = await ResolveProviderAsync(
            patient, settings, insuranceData.Payer.PayEligibilityPhyID, cancellationToken);

        return new EligibilityBuildContext
        {
            Receiver = receiver,
            Patient = patient,
            Insured = insuranceData.Subscriber,
            Payer = insuranceData.Payer,
            Provider = provider,
            Settings = settings,
            UsedPayerOverride = usedPayerOverride
        };
    }

    private async Task<(Physician Physician, bool UsedPayerOverride)> ResolveProviderAsync(
        Patient patient,
        EligibilitySettingsForCheckDto settings,
        int payerProviderId,
        CancellationToken cancellationToken)
    {
        var providerMode = (settings.ProviderMode ?? string.Empty).Trim().ToLowerInvariant();
        var usedPayerOverride = false;
        int providerId;
        switch (providerMode)
        {
            case "billing":
            case "patientbillingprovider":
                providerId = patient.PatBillingPhyFID;
                break;
            case "rendering":
            case "patientrenderingprovider":
                providerId = patient.PatRenderingPhyFID;
                break;
            case "specific":
            case "specificprovider":
                if (settings.SpecificProviderId.GetValueOrDefault() <= 0)
                    throw new InvalidOperationException("Specific Provider is required when Provider Mode is Specific.");
                providerId = settings.SpecificProviderId!.Value;
                break;
            default:
                if (settings.SpecificProviderId.GetValueOrDefault() > 0)
                    providerId = settings.SpecificProviderId!.Value;
                else if (patient.PatBillingPhyFID > 0)
                    providerId = patient.PatBillingPhyFID;
                else if (payerProviderId > 0)
                {
                    providerId = payerProviderId;
                    usedPayerOverride = true;
                }
                else
                    providerId = 0;
                break;
        }

        if (providerId <= 0 && payerProviderId > 0)
        {
            usedPayerOverride = true;
            providerId = payerProviderId;
        }

        if (providerId <= 0)
            throw new InvalidOperationException("Eligibility provider is not configured.");

        var provider = await _db.Physicians
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PhyID == providerId, cancellationToken)
            ?? throw new InvalidOperationException("Eligibility provider not found.");

        if (string.IsNullOrWhiteSpace(provider.PhyNPI))
            throw new InvalidOperationException("Eligibility provider NPI is required.");

        return (provider, usedPayerOverride);
    }

    private async Task<PrimaryInsuranceContext?> LoadPrimaryInsuranceAsync(int patientId, CancellationToken cancellationToken)
    {
        var claimInsurance = await _db.Claims
            .AsNoTracking()
            .Where(c => c.ClaPatFID == patientId)
            .OrderByDescending(c => c.ClaDateTimeModified)
            .ThenByDescending(c => c.ClaID)
            .Select(c => c.ClaID)
            .Join(
                _db.Claim_Insureds
                    .AsNoTracking()
                    .Include(ci => ci.ClaInsPayF)
                    .Where(ci => ci.ClaInsSequence == 1),
                claimId => claimId,
                ci => ci.ClaInsClaFID,
                (_, ci) => ci)
            .Select(ci => new PrimaryInsuranceContext
            {
                Subscriber = new SubscriberSnapshot
                {
                    FirstName = ci.ClaInsFirstName,
                    LastName = ci.ClaInsLastName,
                    MemberId = ci.ClaInsIDNumber
                },
                Payer = ci.ClaInsPayF
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (claimInsurance != null)
            return claimInsurance;

        return await _db.Patient_Insureds
            .AsNoTracking()
            .Where(pi => pi.PatInsPatFID == patientId && pi.PatInsSequence == 1)
            .Include(pi => pi.PatInsIns)
            .ThenInclude(ins => ins.InsPay)
            .Select(pi => new PrimaryInsuranceContext
            {
                Subscriber = new SubscriberSnapshot
                {
                    FirstName = pi.PatInsIns.InsFirstName,
                    LastName = pi.PatInsIns.InsLastName,
                    MemberId = pi.PatInsIns.InsIDNumber
                },
                Payer = pi.PatInsIns.InsPay
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Eligibility270Envelope MapEligibility270Envelope(EligibilityRequest request, EligibilityBuildContext context)
    {
        var interchangeControl = request.ControlNumber.Length > 9
            ? request.ControlNumber[..9]
            : request.ControlNumber.PadLeft(9, '0');
        var groupControl = request.ControlNumber;
        var setControl = request.ControlNumber;

        var submitterId = Required(context.Receiver.SubmitterId, "Submitter ID");
        var receiverName = Required(context.Receiver.ReceiverName, "Receiver name");
        var receiverId = Required(context.Receiver.ReceiverId, "Receiver ID");
        var senderQualifier = Required(context.Receiver.SenderQualifier, "ISA sender qualifier");
        var senderId = Required(context.Receiver.SenderId, "ISA sender ID");
        var receiverQualifier = Required(context.Receiver.ReceiverQualifier, "ISA receiver qualifier");
        var interchangeReceiverId = Required(context.Receiver.InterchangeReceiverId, "ISA receiver ID");
        var authQualifier = Required(context.Receiver.AuthorizationInfoQualifier, "ISA authorization qualifier");
        var secQualifier = Required(context.Receiver.SecurityInfoQualifier, "ISA security qualifier");
        var gsSender = string.IsNullOrWhiteSpace(context.Receiver.SenderCode) ? submitterId : context.Receiver.SenderCode!;
        var gsReceiver = string.IsNullOrWhiteSpace(context.Receiver.ReceiverCode) ? receiverId : context.Receiver.ReceiverCode!;
        var testProd = string.IsNullOrWhiteSpace(context.Receiver.TestProdIndicator) ? "T" : context.Receiver.TestProdIndicator!;

        var providerName = Required(context.Provider.PhyLastName ?? context.Provider.PhyName, "Provider name");
        var subscriberLast = Required(context.Insured.LastName, "Subscriber last name");
        var subscriberFirst = Required(context.Insured.FirstName, "Subscriber first name");
        var subscriberMemberId = Required(context.Insured.MemberId, "Subscriber ID");
        var payerEligibilityId = Required(context.Payer.PayEligibilityPayerID, "Payer eligibility ID");
        if (!context.Patient.PatBirthDate.HasValue)
            throw new InvalidOperationException("Patient date of birth is required for 270 generation.");

        return new Eligibility270Envelope
        {
            InterchangeControlNumber = interchangeControl,
            GroupControlNumber = groupControl,
            TransactionSetControlNumber = setControl,
            AuthInfoQualifier = authQualifier,
            SecurityInfoQualifier = secQualifier,
            SenderQualifier = senderQualifier,
            SenderId = senderId,
            ReceiverQualifier = receiverQualifier,
            InterchangeReceiverId = interchangeReceiverId,
            TestProdIndicator = testProd,
            GsSender = gsSender,
            GsReceiver = gsReceiver,
            SubmitterId = submitterId,
            ReceiverName = receiverName,
            ReceiverId = receiverId,
            ProviderName = providerName,
            ProviderNpi = Required(context.Provider.PhyNPI, "Provider NPI"),
            SubscriberLastName = subscriberLast,
            SubscriberFirstName = subscriberFirst,
            SubscriberMemberId = subscriberMemberId,
            PayerEligibilityId = payerEligibilityId,
            PatientBirthDate = context.Patient.PatBirthDate.Value,
            PatientSex = context.Patient.PatSex
        };
    }

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");
        return value.Trim();
    }

    private static (string Host, int Port) ParseServer(string serverValue)
    {
        var input = serverValue.Trim();
        if (!input.Contains("://", StringComparison.Ordinal))
            input = "sftp://" + input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("Program Setup Patient Eligibility server is invalid.");

        return (uri.Host, uri.Port > 0 ? uri.Port : DefaultSftpPort);
    }

    private static string BuildEligibilityControlNumber()
        => Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();

    private sealed class PrimaryInsuranceContext
    {
        public SubscriberSnapshot Subscriber { get; set; } = null!;
        public Zebl.Infrastructure.Persistence.Entities.Payer Payer { get; set; } = null!;
    }

    private sealed class SubscriberSnapshot
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MemberId { get; set; }
    }

    private sealed class EligibilityBuildContext
    {
        public ReceiverLibrary Receiver { get; set; } = null!;
        public Patient Patient { get; set; } = null!;
        public SubscriberSnapshot Insured { get; set; } = null!;
        public Zebl.Infrastructure.Persistence.Entities.Payer Payer { get; set; } = null!;
        public Physician Provider { get; set; } = null!;
        public EligibilitySettingsForCheckDto Settings { get; set; } = null!;
        public bool UsedPayerOverride { get; set; }
    }

    private static string NormalizeProviderModeDisplay(string? providerMode)
    {
        var m = (providerMode ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(m))
            return "Billing";
        return m.ToLowerInvariant() switch
        {
            "billing" or "patientbillingprovider" => "Billing",
            "rendering" or "patientrenderingprovider" => "Rendering",
            "specific" or "specificprovider" => "Specific",
            _ => m
        };
    }

    private void LogEligibilityOutcome(
        int patientId,
        EligibilityBuildContext context,
        string serverHost,
        int serverPort,
        string outcome,
        string correlationId,
        string? reason)
    {
        _logger.LogInformation(
            "Eligibility outcome -> patient={patientId}, payer={payerId}, receiverId={receiverId}, serverHost={serverHost}, serverPort={serverPort}, providerMode={providerMode}, providerPhyId={providerPhyId}, providerNPI={providerNpi}, usedPayerOverride={usedPayerOverride}, outcome={outcome}, correlationId={correlationId}, reason={reason}",
            patientId,
            context.Payer.PayID,
            context.Settings.ReceiverId,
            serverHost,
            serverPort,
            NormalizeProviderModeDisplay(context.Settings.ProviderMode),
            context.Provider.PhyID,
            context.Provider.PhyNPI,
            context.UsedPayerOverride,
            outcome,
            correlationId,
            reason ?? string.Empty);
    }

    private static async Task<bool> TryTcpReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, linked.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
