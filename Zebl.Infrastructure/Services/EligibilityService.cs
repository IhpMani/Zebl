using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Eligibility 270/271 check. Uses only clearinghouse credentials from patientEligibility settings.
/// Never uses credentials for application login; never logs credentials.
/// </summary>
public class EligibilityService : IEligibilityService
{
    private readonly IEligibilitySettingsProvider _settingsProvider;
    private readonly ZeblDbContext _db;
    private readonly IEdiExportService _ediExportService;
    private readonly EdiReportService _ediReportService;
    private readonly Eligibility271Parser _parser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EligibilityService> _logger;

    public EligibilityService(
        IEligibilitySettingsProvider settingsProvider,
        ZeblDbContext db,
        IEdiExportService ediExportService,
        EdiReportService ediReportService,
        Eligibility271Parser parser,
        IHttpClientFactory httpClientFactory,
        ILogger<EligibilityService> logger)
    {
        _settingsProvider = settingsProvider;
        _db = db;
        _ediExportService = ediExportService;
        _ediReportService = ediReportService;
        _parser = parser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EligibilityCheckResultDto> CheckEligibilityAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetForEligibilityCheckAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.Source))
        {
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Eligibility source is not configured. Configure Program Setup → Patient Eligibility."
            };
        }

        // DEV MODE: mock eligibility
        if (string.Equals(settings.Source.Trim(), "EDIConnection", StringComparison.OrdinalIgnoreCase))
            return await RunMockEligibility(patientId, cancellationToken);

        // Only validate receiver for real clearinghouse sources
        if (!Guid.TryParse(settings.ReceiverId, out var receiverId))
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Eligibility receiver is not configured correctly. Check Program Setup → Patient Eligibility."
            };

        // Load primary insurance for patient with linked payer
        var primaryIns = await _db.Patient_Insureds
            .AsNoTracking()
            .Include(pi => pi.PatInsIns)
                .ThenInclude(i => i.InsPay)
            .Where(pi => pi.PatInsPatFID == patientId)
            .OrderBy(pi => pi.PatInsSequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryIns?.PatInsIns?.InsPay == null)
        {
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Patient does not have a primary insurance with a payer configured."
            };
        }

        var payer = primaryIns.PatInsIns.InsPay;
        var policyNumber = primaryIns.PatInsIns.InsIDNumber ?? string.Empty;
        var identification = primaryIns.PatInsIns.InsIDNumber ?? string.Empty;

        // Insured/Patient fields for the Eligibility Response popup.
        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatID == patientId, cancellationToken);

        if (patient == null)
        {
            _logger.LogWarning("Eligibility check failed: patient {PatientId} not found", patientId);
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Patient record not found."
            };
        }

        var patientLine = BuildPatientLine(patient);
        var patientAddressLine = BuildPatientAddressLine(patient);
        var genderText = MapGender(patient?.PatSex);
        var dob = patient?.PatBirthDate;

        // Generate 270 using existing EDI export infrastructure
        var ansi270 = await _ediExportService.Generate270Async(receiverId);

        // Store outbound 270 in EdiReport so it shows in EDI Reports UI
        var fileName = $"ELIG_270_{patientId}_{DateTime.UtcNow:yyyyMMddHHmmss}.edi";
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(ansi270);
        var ediReport = await _ediReportService.CreateGeneratedAsync(
            receiverId,
            connectionLibraryId: null,
            fileName: fileName,
            fileType: "270",
            fileContent: fileBytes,
            direction: "Outbound");

        // Create EligibilityRequest record
        var request = new EligibilityRequest
        {
            PatientId = patientId,
            PayerId = payer.PayID,
            PolicyNumber = policyNumber,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            EdiReportId = ediReport.Id
        };

        _db.EligibilityRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        // In a real clearinghouse integration, we would send ansi270 using connection settings,
        // wait for 271, and persist the real response. For now, simulate a 271 payload so the
        // storage, parsing, and history pipeline is fully wired.
        var simulated271 = BuildSimulated271(ansi270, payer.PayEligibilityPayerID, policyNumber);
        var parsed = _parser.Parse(simulated271);

        var response = new EligibilityResponse
        {
            EligibilityRequestId = request.Id,
            CoverageStatus = parsed.CoverageStatus,
            PlanName = parsed.PlanName,
            DeductibleAmount = parsed.DeductibleAmount,
            CopayAmount = parsed.CopayAmount,
            CoinsurancePercent = parsed.CoinsurancePercent,
            CoverageStartDate = parsed.CoverageStartDate,
            CoverageEndDate = parsed.CoverageEndDate,
            Raw271 = simulated271,
            CreatedAt = DateTime.UtcNow
        };

        _db.EligibilityResponses.Add(response);

        // Update request status and received time
        request.Status = "ResponseReceived";
        request.ResponseReceivedAt = DateTime.UtcNow;

        // Persist raw ANSI and status on Patient_Insured for quick display if desired
        var primaryTracked = await _db.Patient_Insureds
            .FirstOrDefaultAsync(pi => pi.PatInsGUID == primaryIns.PatInsGUID, cancellationToken);
        if (primaryTracked != null)
        {
            primaryTracked.PatInsEligANSI = simulated271;
            primaryTracked.PatInsEligStatus = parsed.CoverageStatus;
            primaryTracked.PatInsEligDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new EligibilityCheckResultDto
        {
            Success = true,
            Raw271 = simulated271,
            Message = "Eligibility check completed.",
            PayerName = payer.PayName,
            Status = parsed.CoverageStatus ?? "Unknown",
            DeductibleAmount = parsed.DeductibleAmount,
            CopayAmount = parsed.CopayAmount,
            CoinsurancePercent = parsed.CoinsurancePercent,
            CoverageStartDate = parsed.CoverageStartDate,
            CoverageEndDate = parsed.CoverageEndDate,

            PatientName = patientLine,
            PatientAddress = patientAddressLine,
            Identification = identification,
            DateOfBirth = dob,
            Gender = genderText,
            EligibilityDate = primaryTracked?.PatInsEligDate,
            InquiryDate = DateOnly.FromDateTime(request.CreatedAt)
        };
    }

    private async Task<EligibilityCheckResultDto> RunMockEligibility(int patientId, CancellationToken cancellationToken)
    {
        var primaryIns = await _db.Patient_Insureds
            .Include(pi => pi.PatInsIns)
                .ThenInclude(i => i.InsPay)
            .Where(pi => pi.PatInsPatFID == patientId)
            .OrderBy(pi => pi.PatInsSequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryIns == null)
        {
            _logger.LogWarning("Mock eligibility failed: patient {PatientId} has no insurance", patientId);
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Patient has no insurance configured."
            };
        }

        var payer = primaryIns.PatInsIns?.InsPay;
        var identification = primaryIns.PatInsIns?.InsIDNumber;

        // Insured/Patient fields for the Eligibility Response popup.
        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatID == patientId, cancellationToken);

        if (patient == null)
        {
            _logger.LogWarning("Mock eligibility failed: patient {PatientId} not found", patientId);
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Patient record not found."
            };
        }

        var patientLine = BuildPatientLine(patient);
        var patientAddressLine = BuildPatientAddressLine(patient);
        var genderText = MapGender(patient?.PatSex);
        var dob = patient?.PatBirthDate;

        _logger.LogInformation("Running mock eligibility for patient {PatientId} payer {Payer}", patientId, payer?.PayName);

        // Stable simulated 271 (development/test) with Active coverage.
        // NOTE: Eligibility271Parser is intentionally not modified.
        var simulated271 =
            "EB*1*IND*30***23~" +
            "DTP*291*D8*20260101~";

        var coverageStatus = "Active";

        var parsed = _parser.Parse(simulated271);

        var request = new EligibilityRequest
        {
            PatientId = patientId,
            // EligibilityRequest.PayerId is non-nullable in the domain/DB model.
            // For mock/dev mode we allow missing payer linkage by persisting 0.
            PayerId = payer?.PayID ?? 0,
            PolicyNumber = primaryIns.PatInsIns?.InsIDNumber,
            Status = "ResponseReceived",
            CreatedAt = DateTime.UtcNow,
            ResponseReceivedAt = DateTime.UtcNow
        };

        _db.EligibilityRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new EligibilityResponse
        {
            EligibilityRequestId = request.Id,
            CoverageStatus = coverageStatus,
            PlanName = parsed.PlanName,
            DeductibleAmount = parsed.DeductibleAmount,
            CopayAmount = parsed.CopayAmount,
            CoinsurancePercent = parsed.CoinsurancePercent,
            CoverageStartDate = parsed.CoverageStartDate,
            CoverageEndDate = parsed.CoverageEndDate,
            Raw271 = simulated271,
            CreatedAt = DateTime.UtcNow
        };

        _db.EligibilityResponses.Add(response);

        primaryIns.PatInsEligANSI = simulated271;
        primaryIns.PatInsEligStatus = coverageStatus;
        primaryIns.PatInsEligDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        return new EligibilityCheckResultDto
        {
            Success = true,
            Raw271 = simulated271,
            Message = "Mock eligibility check completed.",
            PayerName = payer?.PayName,
            Status = coverageStatus,
            DeductibleAmount = parsed.DeductibleAmount,
            CopayAmount = parsed.CopayAmount,
            CoinsurancePercent = parsed.CoinsurancePercent,
            CoverageStartDate = parsed.CoverageStartDate,
            CoverageEndDate = parsed.CoverageEndDate,

            PatientName = patientLine,
            PatientAddress = patientAddressLine,
            Identification = identification,
            DateOfBirth = dob,
            Gender = genderText,
            EligibilityDate = primaryIns.PatInsEligDate,
            InquiryDate = DateOnly.FromDateTime(request.CreatedAt)
        };
    }

    private static string? MapGender(string? patSex)
    {
        if (string.IsNullOrWhiteSpace(patSex))
            return null;

        return patSex.Trim().ToUpperInvariant() switch
        {
            "M" => "Male",
            "F" => "Female",
            _ => null
        };
    }

    private static string BuildPatientLine(Patient? patient)
    {
        if (patient == null)
            return string.Empty;

        var lastName = patient.PatLastName?.ToUpperInvariant() ?? string.Empty;
        var address = patient.PatAddress?.ToUpperInvariant() ?? string.Empty;
        var city = patient.PatCity?.ToUpperInvariant() ?? string.Empty;
        var state = patient.PatState?.ToUpperInvariant() ?? string.Empty;
        var zip = patient.PatZip ?? string.Empty;

        // EZClaim-like header line:
        // PATIENT BROOKS, 121212 S MAIN AVE, ANYWHERE, NY 33333
        return $"PATIENT {lastName}, {address}, {city}, {state} {zip}".Trim();
    }

    private static string BuildPatientAddressLine(Patient? patient)
    {
        if (patient == null)
            return string.Empty;

        return $"{patient.PatAddress?.ToUpperInvariant() ?? string.Empty}, {patient.PatCity?.ToUpperInvariant() ?? string.Empty}, {patient.PatState?.ToUpperInvariant() ?? string.Empty} {patient.PatZip}".Trim();
    }

    public async Task<EligibilityHistoryItemDto[]> GetHistoryAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var query =
            from req in _db.EligibilityRequests.AsNoTracking()
            join res in _db.EligibilityResponses.AsNoTracking()
                on req.Id equals res.EligibilityRequestId into resJoin
            from res in resJoin.OrderByDescending(r => r.CreatedAt).Take(1).DefaultIfEmpty()
            where req.PatientId == patientId
            orderby req.CreatedAt descending
            select new EligibilityHistoryItemDto
            {
                RequestId = req.Id,
                CheckDate = req.CreatedAt,
                Status = req.Status,
                CoverageStatus = res.CoverageStatus,
                PlanName = res.PlanName,
                DeductibleAmount = res.DeductibleAmount,
                CopayAmount = res.CopayAmount,
                CoinsurancePercent = res.CoinsurancePercent,
                CoverageStartDate = res.CoverageStartDate,
                CoverageEndDate = res.CoverageEndDate
            };

        return await query.ToArrayAsync(cancellationToken);
    }

    private static string BuildSimulated271(string ansi270, string? payerEligibilityId, string policyNumber)
    {
        var now = DateTime.UtcNow;
        var today = now.ToString("yyyyMMdd");
        var start = now.Date.ToString("yyyyMMdd");
        var end = now.Date.AddYears(1).ToString("yyyyMMdd");

        // Very small 271 stub reusing ISA/GS envelope from 270 when possible.
        // If we can't reliably reuse, just append minimal segments.
        var sb = new System.Text.StringBuilder();
        sb.Append(ansi270);
        sb.Append("ST*271*0001~");
        sb.Append("HL*1**20*1~");
        sb.Append("NM1*PR*2*").Append(payerEligibilityId ?? "PAYER").Append("*****PI*").Append(payerEligibilityId ?? "PAYERID").Append("~");
        sb.Append("NM1*IL*1*MEMBER*LAST****MI*").Append(policyNumber).Append("~");
        sb.Append("EB*1*IND*PLAN*Standard Plan*25.00*500.00*0.2***Y~");
        sb.Append("DTP*291*D8*").Append(start).Append("~");
        sb.Append("DTP*292*D8*").Append(end).Append("~");
        sb.Append("SE*7*0001~");
        return sb.ToString();
    }
}
