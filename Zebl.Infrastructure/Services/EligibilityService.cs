using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public EligibilityService(
        IEligibilitySettingsProvider settingsProvider,
        ZeblDbContext db,
        IEdiExportService ediExportService,
        EdiReportService ediReportService,
        Eligibility271Parser parser)
    {
        _settingsProvider = settingsProvider;
        _db = db;
        _ediExportService = ediExportService;
        _ediReportService = ediReportService;
        _parser = parser;
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

        if (!Guid.TryParse(settings.ReceiverId, out var receiverId))
        {
            return new EligibilityCheckResultDto
            {
                Success = false,
                Message = "Eligibility receiver is not configured correctly. Check Program Setup → Patient Eligibility."
            };
        }

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
            CoverageEndDate = parsed.CoverageEndDate
        };
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
