using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly ZeblDbContext _context;

    public ClaimRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimData?> GetByIdAsync(int claimId)
    {
        var claim = await _context.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == claimId);
            
        if (claim == null)
            return null;
            
        return new ClaimData
        {
            ClaimId = claim.ClaID
        };
    }

    public async Task UpdateSubmissionStatusAsync(int claimId, string submissionMethod, string status, DateTime lastExportedDate)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null) return;
        claim.ClaSubmissionMethod = submissionMethod;
        claim.ClaStatus = status;
        claim.ClaLastExportedDate = DateOnly.FromDateTime(lastExportedDate);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateClaimStatusAsync(int claimId, string status)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null) return;
        claim.ClaStatus = status ?? claim.ClaStatus;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTotalsAsync(int claimId, ClaimTotals totals)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null) return;
        claim.ClaTotalChargeTRIG = totals.TotalCharge;
        claim.ClaTotalInsAmtPaidTRIG = totals.TotalInsAmtPaid;
        claim.ClaTotalPatAmtPaidTRIG = totals.TotalPatAmtPaid;
        claim.ClaTotalCOAdjTRIG = totals.TotalCOAdj;
        claim.ClaTotalCRAdjTRIG = totals.TotalCRAdj;
        claim.ClaTotalOAAdjTRIG = totals.TotalOAAdj;
        claim.ClaTotalPIAdjTRIG = totals.TotalPIAdj;
        claim.ClaTotalPRAdjTRIG = totals.TotalPRAdj;
        decimal totalAdj = totals.TotalCOAdj + totals.TotalCRAdj + totals.TotalOAAdj + totals.TotalPIAdj + totals.TotalPRAdj;
        claim.ClaTotalBalanceCC = totals.TotalCharge - totals.TotalInsAmtPaid - totals.TotalPatAmtPaid - totalAdj;
        await _context.SaveChangesAsync();
    }

    public async Task<int?> GetBillingPhysicianIdAsync(int claimId)
    {
        var c = await _context.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.ClaID == claimId);
        return c?.ClaBillingPhyFID;
    }

    public async Task<ClaimSecondaryEvalData?> GetClaimForSecondaryEvalAsync(int claimId)
    {
        var c = await _context.Claims
            .AsNoTracking()
            .Include(x => x.Claim_Insureds)
            .FirstOrDefaultAsync(x => x.ClaID == claimId);
        if (c == null) return null;
        var primary = c.Claim_Insureds?.FirstOrDefault(ci => ci.ClaInsSequence == 1);
        var secondary = c.Claim_Insureds?.FirstOrDefault(ci => ci.ClaInsSequence == 2);
        decimal balance = c.ClaTotalBalanceCC ?? (c.ClaTotalChargeTRIG - c.ClaTotalInsAmtPaidTRIG - c.ClaTotalPatAmtPaidTRIG
            - (c.ClaTotalCOAdjTRIG ?? 0) - (c.ClaTotalCRAdjTRIG ?? 0) - (c.ClaTotalOAAdjTRIG ?? 0) - (c.ClaTotalPIAdjTRIG ?? 0) - (c.ClaTotalPRAdjTRIG ?? 0));
        return new ClaimSecondaryEvalData
        {
            ClaimId = c.ClaID,
            PatientId = c.ClaPatFID,
            Status = c.ClaStatus,
            TotalBalance = balance,
            PrimaryPayerId = primary?.ClaInsPayFID ?? 0,
            SecondaryPayerId = secondary?.ClaInsPayFID,
            BillingPhysicianId = c.ClaBillingPhyFID,
            RenderingPhysicianId = c.ClaRenderingPhyFID,
            AttendingPhysicianId = c.ClaAttendingPhyFID,
            FacilityPhysicianId = c.ClaFacilityPhyFID,
            ReferringPhysicianId = c.ClaReferringPhyFID,
            SupervisingPhysicianId = c.ClaSupervisingPhyFID,
            OperatingPhysicianId = c.ClaOperatingPhyFID,
            OrderingPhysicianId = c.ClaOrderingPhyFID,
            BillDate = c.ClaBillDate,
            Diagnosis1 = c.ClaDiagnosis1,
            Diagnosis2 = c.ClaDiagnosis2,
            Diagnosis3 = c.ClaDiagnosis3,
            Diagnosis4 = c.ClaDiagnosis4,
            Diagnosis5 = c.ClaDiagnosis5,
            ICDIndicator = c.ClaICDIndicator,
            SubmissionMethod = c.ClaSubmissionMethod,
            ClaimType = c.ClaClaimType,
            PrimaryClaimFID = c.ClaPrimaryClaimFID
        };
    }

    public async Task<List<(string GroupCode, string? ReasonCode, decimal Amount)>> GetAdjustmentsByClaimIdAsync(int claimId)
    {
        var srvIds = await _context.Service_Lines
            .AsNoTracking()
            .Where(s => s.SrvClaFID == claimId)
            .Select(s => s.SrvID)
            .ToListAsync();
        if (srvIds.Count == 0) return new List<(string, string?, decimal)>();
        var list = await _context.Adjustments
            .AsNoTracking()
            .Where(a => srvIds.Contains(a.AdjSrvFID))
            .Select(a => new { a.AdjGroupCode, a.AdjReasonCode, a.AdjAmount })
            .ToListAsync();
        return list.Select(a => (a.AdjGroupCode ?? "", a.AdjReasonCode, a.AdjAmount)).ToList();
    }

    public async Task<bool> ExistsSecondaryForPrimaryAsync(int primaryClaimId)
    {
        return await _context.Claims
            .AsNoTracking()
            .AnyAsync(c => c.ClaPrimaryClaimFID == primaryClaimId && c.ClaClaimType == "Secondary");
    }

    public async Task<int> CreateSecondaryClaimAsync(int primaryClaimId, int secondaryPayerId, decimal forwardAmount, ClaimSecondaryEvalData fromPrimary)
    {
        var primary = await _context.Claims
            .Include(c => c.Claim_Insureds.OrderBy(ci => ci.ClaInsSequence))
            .Include(c => c.Service_Lines.OrderBy(s => s.SrvID))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == primaryClaimId);
        if (primary == null) throw new InvalidOperationException("Primary claim not found.");
        var firstLine = primary.Service_Lines?.FirstOrDefault();
        var primaryInsured = primary.Claim_Insureds?.FirstOrDefault(ci => ci.ClaInsSequence == 1);
        var now = DateTime.UtcNow;
        var newClaim = new Claim
        {
            ClaID = 0,
            ClaDateTimeCreated = now,
            ClaDateTimeModified = now,
            ClaClaimType = "Secondary",
            ClaPrimaryClaimFID = primaryClaimId,
            ClaPatFID = primary.ClaPatFID,
            ClaBillingPhyFID = primary.ClaBillingPhyFID,
            ClaRenderingPhyFID = primary.ClaRenderingPhyFID,
            ClaAttendingPhyFID = primary.ClaAttendingPhyFID,
            ClaFacilityPhyFID = primary.ClaFacilityPhyFID,
            ClaReferringPhyFID = primary.ClaReferringPhyFID,
            ClaSupervisingPhyFID = primary.ClaSupervisingPhyFID,
            ClaOperatingPhyFID = primary.ClaOperatingPhyFID,
            ClaOrderingPhyFID = primary.ClaOrderingPhyFID,
            ClaBillDate = primary.ClaBillDate,
            ClaDiagnosis1 = primary.ClaDiagnosis1,
            ClaDiagnosis2 = primary.ClaDiagnosis2,
            ClaDiagnosis3 = primary.ClaDiagnosis3,
            ClaDiagnosis4 = primary.ClaDiagnosis4,
            ClaDiagnosis5 = primary.ClaDiagnosis5,
            ClaICDIndicator = primary.ClaICDIndicator,
            ClaSubmissionMethod = primary.ClaSubmissionMethod ?? "Electronic",
            ClaStatus = "ReadyToSubmit",
            ClaTotalChargeTRIG = forwardAmount,
            ClaTotalInsAmtPaidTRIG = 0,
            ClaTotalPatAmtPaidTRIG = 0,
            ClaTotalCOAdjTRIG = 0,
            ClaTotalCRAdjTRIG = 0,
            ClaTotalOAAdjTRIG = 0,
            ClaTotalPIAdjTRIG = 0,
            ClaTotalPRAdjTRIG = 0,
            ClaTotalServiceLineCountTRIG = 1,
            ClaTotalInsBalanceTRIG = forwardAmount,
            ClaTotalPatBalanceTRIG = 0
        };
        _context.Claims.Add(newClaim);
        await _context.SaveChangesAsync();
        var newClaimId = newClaim.ClaID;

        var newInsured = new Claim_Insured
        {
            ClaInsClaFID = newClaimId,
            ClaInsPatFID = primary.ClaPatFID,
            ClaInsPayFID = secondaryPayerId,
            ClaInsSequence = 2,
            ClaInsDateTimeCreated = now,
            ClaInsDateTimeModified = now,
            ClaInsRelationToInsured = primaryInsured?.ClaInsRelationToInsured ?? 1,
            ClaInsFirstName = primaryInsured?.ClaInsFirstName,
            ClaInsLastName = primaryInsured?.ClaInsLastName,
            ClaInsIDNumber = primaryInsured?.ClaInsIDNumber,
            ClaInsGroupNumber = primaryInsured?.ClaInsGroupNumber,
            ClaInsBirthDate = primaryInsured?.ClaInsBirthDate,
            ClaInsAddress = primaryInsured?.ClaInsAddress,
            ClaInsCity = primaryInsured?.ClaInsCity,
            ClaInsState = primaryInsured?.ClaInsState,
            ClaInsZip = primaryInsured?.ClaInsZip,
            ClaInsSex = primaryInsured?.ClaInsSex,
            ClaInsClaimFilingIndicator = primaryInsured?.ClaInsClaimFilingIndicator
        };
        _context.Claim_Insureds.Add(newInsured);

        var fromDate = firstLine?.SrvFromDate ?? primary.ClaBillDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = firstLine?.SrvToDate ?? fromDate;
        var newLine = new Service_Line
        {
            SrvClaFID = newClaimId,
            SrvCharges = forwardAmount,
            SrvFromDate = fromDate,
            SrvToDate = toDate,
            SrvGUID = Guid.NewGuid(),
            SrvDateTimeCreated = now,
            SrvDateTimeModified = now,
            SrvResponsibleParty = secondaryPayerId,
            SrvRespChangeDate = DateTime.UtcNow,
            SrvProcedureCode = firstLine?.SrvProcedureCode ?? "ZZZ",
            SrvPlace = firstLine?.SrvPlace,
            SrvDiagnosisPointer = firstLine?.SrvDiagnosisPointer ?? "1",
            SrvSortTiebreaker = 1,
            SrvTotalInsAmtPaidTRIG = 0,
            SrvTotalPatAmtPaidTRIG = 0,
            SrvTotalCOAdjTRIG = 0,
            SrvTotalCRAdjTRIG = 0,
            SrvTotalOAAdjTRIG = 0,
            SrvTotalPIAdjTRIG = 0,
            SrvTotalPRAdjTRIG = 0
        };
        _context.Service_Lines.Add(newLine);
        await _context.SaveChangesAsync();
        return newClaimId;
    }
}
