using System.Text;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// 837 claim export with Payer rules. No DbContext; uses IClaimExportDataProvider and IClaimRepository.
/// </summary>
public class ClaimExportService : IClaimExportService
{
    private readonly IClaimExportDataProvider _dataProvider;
    private readonly IClaimRepository _claimRepo;

    public ClaimExportService(IClaimExportDataProvider dataProvider, IClaimRepository claimRepo)
    {
        _dataProvider = dataProvider;
        _claimRepo = claimRepo;
    }

    public async Task<string> Generate837Async(int claimId)
    {
        var data = await _dataProvider.GetExportDataAsync(claimId)
            ?? throw new InvalidOperationException("Claim not found.");

        if (data.Payer == null)
            throw new InvalidOperationException("Payer not found for this claim. Ensure primary insured has a payer.");

        var payer = data.Payer;

        // RULE A — Submission Method
        if (!string.Equals(payer.PaySubmissionMethod, "Electronic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This payer is configured for Paper submission.");

        // RULE B — Payer ID required for electronic
        if (string.IsNullOrWhiteSpace(payer.PayExternalID))
            throw new InvalidOperationException("Payer ID is required for electronic submission.");

        // RULE C — Claim Filing Indicator: Payer override else Claim_Insured
        var claimFilingIndicator = !string.IsNullOrWhiteSpace(payer.PayClaimFilingIndicator)
            ? payer.PayClaimFilingIndicator
            : data.PrimaryInsured?.ClaInsClaimFilingIndicator;

        // RULE D — Insurance Type Code: Claim override else Payer else omit
        var insuranceTypeCode = !string.IsNullOrWhiteSpace(data.ClaInsuranceTypeCodeOverride)
            ? data.ClaInsuranceTypeCodeOverride
            : payer.PayInsTypeCode;

        // Build 837 with conditional segments (RULE E)
        var content = Build837(data, claimFilingIndicator, insuranceTypeCode, payer);

        // Status transition after successful export
        await _claimRepo.UpdateSubmissionStatusAsync(claimId, "Electronic", "Submitted", DateTime.UtcNow);

        return content;
    }

    private static string Build837(
        Claim837ExportData data,
        string? claimFilingIndicator,
        string? insuranceTypeCode,
        Payer payer)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now;

        // ISA
        sb.Append("ISA*00*          *00*          *ZZ*")
          .Append((payer.PayExternalID ?? "").PadRight(15))
          .Append("*ZZ*")
          .Append((payer.PayExternalID ?? "").PadRight(15))
          .Append("*")
          .Append(now.ToString("yyMMdd"))
          .Append("*")
          .Append(now.ToString("HHmm"))
          .Append("*^*00501*000000001*0*P*:~");

        // GS
        sb.Append("GS*HC*")
          .Append((payer.PayExternalID ?? "").PadRight(15))
          .Append("*")
          .Append((payer.PayExternalID ?? "").PadRight(15))
          .Append("*")
          .Append(now.ToString("yyyyMMdd"))
          .Append("*")
          .Append(now.ToString("HHmm"))
          .Append("*1*X*005010X222A1~");

        // ST
        sb.Append("ST*837*0001~");

        // BHT
        sb.Append("BHT*0019*00*")
          .Append(now.ToString("yyyyMMddHHmm"))
          .Append("*")
          .Append(now.ToString("yyyyMMdd"))
          .Append("*")
          .Append(now.ToString("HHmm"))
          .Append("*CH~");

        // 1000A Submitter
        sb.Append("NM1*41*2*SUBMITTER*****46*")
          .Append((payer.PayExternalID ?? "").Trim())
          .Append("~");

        // 1000B Receiver
        sb.Append("NM1*40*2*RECEIVER*****46*")
          .Append((payer.PayExternalID ?? "").Trim())
          .Append("~");

        // 2000A Billing Provider (always)
        if (data.BillingProvider != null)
        {
            sb.Append("HL*1* *20*1~");
            sb.Append("PRV*BI*PXC*");
            sb.Append(Escape(data.BillingProvider.PhyNPI ?? ""));
            sb.Append("~");
            sb.Append("NM1*85*2*");
            sb.Append(Escape(data.BillingProvider.PhyLastName ?? ""));
            sb.Append("*");
            sb.Append(Escape(data.BillingProvider.PhyFirstName ?? ""));
            sb.Append("~");
        }

        // 2000B Subscriber (primary insured)
        if (data.PrimaryInsured != null)
        {
            sb.Append("HL*2*1*22*0~");
            sb.Append("SBR*P*18*");
            sb.Append(Escape(data.PrimaryInsured.ClaInsGroupNumber ?? ""));
            sb.Append("******");
            sb.Append(Escape(claimFilingIndicator ?? ""));
            sb.Append("~"); // SBR09 - RULE C

            // 2010BB Payer
            sb.Append("NM1*PR*2*").Append(Escape(payer.PayName ?? "")).Append("~");
            sb.Append("N3*").Append(Escape(payer.PayAddr1 ?? "")).Append("~");
            sb.Append("N4*").Append(Escape(payer.PayCity ?? "")).Append("*").Append(Escape(payer.PayState ?? "")).Append("*").Append(Escape(payer.PayZip ?? "")).Append("~");

            // 2010BA Subscriber — RULE E: PayExportSSN → export SSN in 2010BA
            sb.Append("NM1*IL*1*")
              .Append(Escape(data.PrimaryInsured.ClaInsLastName ?? ""))
              .Append("*")
              .Append(Escape(data.PrimaryInsured.ClaInsFirstName ?? ""))
              .Append("~");
            if (data.PrimaryInsured.ClaInsBirthDate.HasValue || !string.IsNullOrWhiteSpace(data.PrimaryInsured.ClaInsSex))
                sb.Append("DMG*D8*").Append(FormatDate(data.PrimaryInsured.ClaInsBirthDate)).Append("*").Append(Escape(data.PrimaryInsured.ClaInsSex ?? "")).Append("~");
            if (payer.PayExportSSN && !string.IsNullOrWhiteSpace(data.PrimaryInsured.ClaInsSSN))
                sb.Append("REF*SY*").Append(Escape(data.PrimaryInsured.ClaInsSSN)).Append("~");
        }

        // 2000C Patient
        if (data.Patient != null)
        {
            sb.Append("HL*3*2*23*0~");
            sb.Append("NM1*QC*1*")
              .Append(Escape(data.Patient.PatLastName ?? ""))
              .Append("*")
              .Append(Escape(data.Patient.PatFirstName ?? ""))
              .Append("*")
              .Append(Escape(data.Patient.PatMI ?? ""))
              .Append("~");
            sb.Append("DMG*D8*").Append(FormatDate(data.Patient.PatBirthDate)).Append("*").Append(Escape(data.Patient.PatSex ?? "")).Append("~");
        }

        // 2300 Claim — RULE E: conditional loops. Insurance type (RULE D) used in 2000B/segments as needed; CLM is id + amount.
        sb.Append("CLM*").Append(data.ClaimId).Append("*0~");
        if (!string.IsNullOrWhiteSpace(insuranceTypeCode))
            sb.Append("REF*EL*").Append(Escape(insuranceTypeCode)).Append("~");

        // 2400/2300 Authorization — RULE E: PayExportAuthIn2400 → Loop 2400 else 2300
        if (payer.PayExportAuthIn2400)
            sb.Append("REF*G1*").Append(Escape(data.ClaReferralNumber ?? "")).Append("~"); // in 2400
        else if (!string.IsNullOrWhiteSpace(data.ClaReferralNumber))
            sb.Append("REF*G1*").Append(Escape(data.ClaReferralNumber)).Append("~"); // in 2300

        // 2330B — RULE E: PayExportOriginalRefIn2330B
        if (payer.PayExportOriginalRefIn2330B && !string.IsNullOrWhiteSpace(data.ClaReferralNumber))
            sb.Append("REF*1L*").Append(Escape(data.ClaReferralNumber)).Append("~");

        // Rendering provider — RULE E: PayIgnoreRenderingProvider → suppress
        if (!payer.PayIgnoreRenderingProvider && data.RenderingProvider != null)
        {
            sb.Append("NM1*82*1*")
              .Append(Escape(data.RenderingProvider.PhyLastName ?? ""))
              .Append("*")
              .Append(Escape(data.RenderingProvider.PhyFirstName ?? ""))
              .Append("*****XX*")
              .Append(Escape(data.RenderingProvider.PhyNPI ?? ""))
              .Append("~");
        }

        // 2430 — RULE E: PayExportPatientAmtDueIn2430 → AMT*EAF
        if (payer.PayExportPatientAmtDueIn2430)
            sb.Append("AMT*EAF*0~");

        // SE/GE/IEA
        var segmentCount = sb.ToString().Count(c => c == '~') + 1;
        sb.Append("SE*").Append(segmentCount).Append("*0001~");
        sb.Append("GE*1*1~");
        sb.Append("IEA*1*000000001~");

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("*", "").Replace("~", "").Replace("^", "").Trim();
    }

    private static string FormatDate(DateOnly? d)
    {
        return d?.ToString("yyyyMMdd") ?? "";
    }
}
