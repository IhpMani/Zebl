using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using PayerEntity = Zebl.Infrastructure.Persistence.Entities.Payer;
using PayerDomain = Zebl.Application.Domain.Payer;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Loads claim, primary insured, patient, payer, and providers for 837 export. Data access only.
/// </summary>
public class ClaimExportDataProvider : IClaimExportDataProvider
{
    private readonly ZeblDbContext _context;

    public ClaimExportDataProvider(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<Claim837ExportData?> GetExportDataAsync(int claimId)
    {
        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.Claim_Insureds.Where(ci => ci.ClaInsSequence == 1))
            .Include(c => c.ClaPatF)
            .Include(c => c.ClaBillingPhyF)
            .Include(c => c.ClaRenderingPhyF)
            .FirstOrDefaultAsync(c => c.ClaID == claimId);

        if (claim == null)
            return null;

        var primaryInsured = claim.Claim_Insureds?.FirstOrDefault(ci => ci.ClaInsSequence == 1);
        if (primaryInsured == null)
            return null;

        PayerEntity? payerEntity = null;
        if (primaryInsured.ClaInsPayFID != 0)
            payerEntity = await _context.Payers.AsNoTracking().FirstOrDefaultAsync(p => p.PayID == primaryInsured.ClaInsPayFID);

        var result = new Claim837ExportData
        {
            ClaimId = claim.ClaID,
            ClaInsuranceTypeCodeOverride = claim.ClaInsuranceTypeCodeOverride,
            ClaBillDate = claim.ClaBillDate,
            ClaStatementCoversFromOverride = claim.ClaStatementCoversFromOverride,
            ClaStatementCoversThroughOverride = claim.ClaStatementCoversThroughOverride,
            ClaDiagnosis1 = claim.ClaDiagnosis1,
            ClaDiagnosis2 = claim.ClaDiagnosis2,
            ClaReferralNumber = claim.ClaReferralNumber,
            PrimaryInsured = new ClaimInsuredExportDto
            {
                ClaInsClaimFilingIndicator = primaryInsured.ClaInsClaimFilingIndicator,
                ClaInsSSN = primaryInsured.ClaInsSSN,
                ClaInsFirstName = primaryInsured.ClaInsFirstName,
                ClaInsLastName = primaryInsured.ClaInsLastName,
                ClaInsIDNumber = primaryInsured.ClaInsIDNumber,
                ClaInsGroupNumber = primaryInsured.ClaInsGroupNumber,
                ClaInsBirthDate = primaryInsured.ClaInsBirthDate,
                ClaInsAddress = primaryInsured.ClaInsAddress,
                ClaInsCity = primaryInsured.ClaInsCity,
                ClaInsState = primaryInsured.ClaInsState,
                ClaInsZip = primaryInsured.ClaInsZip,
                ClaInsSex = primaryInsured.ClaInsSex,
                ClaInsSequence = primaryInsured.ClaInsSequence
            },
            Patient = claim.ClaPatF == null ? null : new PatientExportDto
            {
                PatFirstName = claim.ClaPatF.PatFirstName,
                PatLastName = claim.ClaPatF.PatLastName,
                PatMI = claim.ClaPatF.PatMI,
                PatBirthDate = claim.ClaPatF.PatBirthDate,
                PatAddress = claim.ClaPatF.PatAddress,
                PatCity = claim.ClaPatF.PatCity,
                PatState = claim.ClaPatF.PatState,
                PatZip = claim.ClaPatF.PatZip,
                PatSex = claim.ClaPatF.PatSex
            },
            Payer = payerEntity == null ? null : MapPayerToDomain(payerEntity),
            BillingProvider = claim.ClaBillingPhyF == null ? null : MapPhysician(claim.ClaBillingPhyF),
            RenderingProvider = claim.ClaRenderingPhyF == null ? null : MapPhysician(claim.ClaRenderingPhyF)
        };

        return result;
    }

    private static PayerDomain MapPayerToDomain(PayerEntity e)
    {
        return new PayerDomain
        {
            PayID = e.PayID,
            PayName = e.PayName,
            PayExternalID = e.PayExternalID,
            PayAddr1 = e.PayAddr1,
            PayCity = e.PayCity,
            PayState = e.PayState,
            PayZip = e.PayZip,
            PaySubmissionMethod = e.PaySubmissionMethod ?? "Paper",
            PayClaimFilingIndicator = e.PayClaimFilingIndicator,
            PayClaimType = e.PayClaimType ?? "Professional",
            PayInsTypeCode = e.PayInsTypeCode,
            PayExportAuthIn2400 = e.PayExportAuthIn2400,
            PayExportOriginalRefIn2330B = e.PayExportOriginalRefIn2330B,
            PayExportPaymentDateIn2330B = e.PayExportPaymentDateIn2330B,
            PayExportPatientAmtDueIn2430 = e.PayExportPatientAmtDueIn2430,
            PayExportSSN = e.PayExportSSN,
            PayIgnoreRenderingProvider = e.PayIgnoreRenderingProvider
        };
    }

    private static ProviderExportDto MapPhysician(Physician phy)
    {
        return new ProviderExportDto
        {
            PhyNPI = phy.PhyNPI,
            PhyName = phy.PhyName,
            PhyFirstName = phy.PhyFirstName,
            PhyLastName = phy.PhyLastName,
            PhyAddress1 = phy.PhyAddress1,
            PhyCity = phy.PhyCity,
            PhyState = phy.PhyState,
            PhyZip = phy.PhyZip
        };
    }
}
