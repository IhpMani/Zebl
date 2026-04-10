using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using Zebl.Infrastructure.Services;

namespace Zebl.Infrastructure.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly ZeblDbContext _context;
    private readonly ClaimInitialStatusProvider _claimInitialStatus;
    private readonly ICurrentContext _currentContext;
    private readonly ICurrentUserContext _currentUserContext;

    public ClaimRepository(
        ZeblDbContext context,
        ClaimInitialStatusProvider claimInitialStatus,
        ICurrentContext currentContext,
        ICurrentUserContext currentUserContext)
    {
        _context = context;
        _claimInitialStatus = claimInitialStatus;
        _currentContext = currentContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<ClaimData?> GetByIdAsync(int claimId)
    {
        var fid = _currentContext.FacilityId;
        var claim = await _context.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == claimId && c.FacilityId == fid);
            
        if (claim == null)
            return null;
            
        return new ClaimData
        {
            ClaimId = claim.ClaID
        };
    }

    public async Task UpdateSubmissionStatusAsync(int claimId, string submissionMethod, string status, DateTime lastExportedDate)
    {
        if (_currentContext.TenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var claim = await _context.Claims.FirstOrDefaultAsync(c => c.ClaID == claimId && c.FacilityId == fid);
        if (claim == null) return;
        claim.ClaSubmissionMethod = submissionMethod;
        claim.ClaStatus = status;
        claim.ClaLastExportedDate = DateOnly.FromDateTime(lastExportedDate);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateClaimStatusAsync(int claimId, string status)
    {
        if (_currentContext.TenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var claim = await _context.Claims.FirstOrDefaultAsync(c => c.ClaID == claimId && c.FacilityId == fid);
        if (claim == null) return;
        claim.ClaStatus = status ?? claim.ClaStatus;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTotalsAsync(int claimId, ClaimTotals totals)
    {
        if (_currentContext.TenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var claim = await _context.Claims.FirstOrDefaultAsync(c => c.ClaID == claimId && c.FacilityId == fid);
        if (claim == null) return;
        var lineRollups = await _context.Service_Lines
            .AsNoTracking()
            .Where(s => s.SrvClaFID == claimId && s.FacilityId == fid)
            .Select(s => new
            {
                s.SrvTotalBalanceCC,
                s.SrvTotalInsBalanceCC,
                s.SrvTotalPatBalanceCC
            })
            .ToListAsync();

        claim.ClaTotalChargeTRIG = totals.TotalCharge;
        claim.ClaTotalInsAmtPaidTRIG = totals.TotalInsAmtPaid;
        claim.ClaTotalPatAmtPaidTRIG = totals.TotalPatAmtPaid;
        claim.ClaTotalCOAdjTRIG = totals.TotalCOAdj;
        claim.ClaTotalCRAdjTRIG = totals.TotalCRAdj;
        claim.ClaTotalOAAdjTRIG = totals.TotalOAAdj;
        claim.ClaTotalPIAdjTRIG = totals.TotalPIAdj;
        claim.ClaTotalPRAdjTRIG = totals.TotalPRAdj;
        decimal totalAdj = totals.TotalCOAdj + totals.TotalCRAdj + totals.TotalOAAdj + totals.TotalPIAdj + totals.TotalPRAdj;
        var computedTotalBalance = totals.TotalCharge - totals.TotalInsAmtPaid - totals.TotalPatAmtPaid - totalAdj;
        claim.ClaTotalBalanceCC = lineRollups.Count > 0
            ? lineRollups.Sum(x => x.SrvTotalBalanceCC ?? 0m)
            : computedTotalBalance;
        claim.ClaTotalInsBalanceTRIG = lineRollups.Count > 0
            ? lineRollups.Sum(x => x.SrvTotalInsBalanceCC ?? 0m)
            : (claim.ClaTotalBalanceCC ?? 0m);
        claim.ClaTotalPatBalanceTRIG = lineRollups.Count > 0
            ? lineRollups.Sum(x => x.SrvTotalPatBalanceCC ?? 0m)
            : 0m;
        await _context.SaveChangesAsync();
    }

    public async Task<int?> GetBillingPhysicianIdAsync(int claimId)
    {
        var fid = _currentContext.FacilityId;
        var c = await _context.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.ClaID == claimId && x.FacilityId == fid);
        return c?.ClaBillingPhyFID;
    }

    public async Task<ClaimSecondaryEvalData?> GetClaimForSecondaryEvalAsync(int claimId)
    {
        var fid = _currentContext.FacilityId;
        var c = await _context.Claims
            .AsNoTracking()
            .Include(x => x.Claim_Insureds)
            .FirstOrDefaultAsync(x => x.ClaID == claimId && x.FacilityId == fid);
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
        var fid = _currentContext.FacilityId;
        var claimOk = await _context.Claims.AsNoTracking().AnyAsync(c => c.ClaID == claimId && c.FacilityId == fid);
        if (!claimOk) return new List<(string, string?, decimal)>();

        var srvIds = await _context.Service_Lines
            .AsNoTracking()
            .Where(s => s.SrvClaFID == claimId)
            .Select(s => s.SrvID)
            .ToListAsync();
        if (srvIds.Count == 0) return new List<(string, string?, decimal)>();
        var list = await _context.Adjustments
            .AsNoTracking()
            .Where(a => srvIds.Contains(a.AdjSrvFID) && a.FacilityId == fid)
            .Select(a => new { a.AdjGroupCode, a.AdjReasonCode, a.AdjAmount })
            .ToListAsync();
        return list.Select(a => (a.AdjGroupCode ?? "", a.AdjReasonCode, a.AdjAmount)).ToList();
    }

    public async Task<bool> ExistsSecondaryForPrimaryAsync(int primaryClaimId)
    {
        var fid = _currentContext.FacilityId;
        return await _context.Claims
            .AsNoTracking()
            .AnyAsync(c =>
                c.ClaPrimaryClaimFID == primaryClaimId &&
                c.ClaClaimType == "Secondary" &&
                c.FacilityId == fid);
    }

    public async Task<int> CreateSecondaryClaimAsync(int primaryClaimId, int secondaryPayerId, decimal forwardAmount, ClaimSecondaryEvalData fromPrimary)
    {
        var currentTenantId = _currentContext.TenantId;
        if (currentTenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var primary = await _context.Claims
            .Include(c => c.Claim_Insureds.OrderBy(ci => ci.ClaInsSequence))
            .Include(c => c.Service_Lines.OrderBy(s => s.SrvID))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == primaryClaimId && c.FacilityId == fid);
        if (primary == null) throw new InvalidOperationException("Primary claim not found.");
        var patient = await _context.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatID == primary.ClaPatFID && p.FacilityId == fid);
        if (patient == null) throw new InvalidOperationException("Patient not found for primary claim.");
        if (patient.TenantId != primary.TenantId)
            throw new InvalidOperationException("Tenant mismatch: Claim parent Patient tenant does not match primary claim tenant.");
        var firstLine = primary.Service_Lines?.FirstOrDefault();
        var primaryInsured = primary.Claim_Insureds?.FirstOrDefault(ci => ci.ClaInsSequence == 1);
        var now = DateTime.UtcNow;
        var initialStatus = await _claimInitialStatus.GetInitialClaStatusStringAsync();
        var newClaim = new Claim
        {
            ClaID = 0,
            TenantId = patient.TenantId,
            FacilityId = primary.FacilityId,
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
            ClaStatus = initialStatus,
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
        if (newClaim.TenantId != primary.TenantId)
            throw new InvalidOperationException("Tenant mismatch: Claim.TenantId must match primary claim tenant.");
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
            TenantId = newClaim.TenantId,
            FacilityId = primary.FacilityId,
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
        if (newLine.TenantId != newClaim.TenantId)
            throw new InvalidOperationException("Tenant mismatch: Service_Line.TenantId must match Claim.TenantId.");
        _context.Service_Lines.Add(newLine);
        await _context.SaveChangesAsync();
        return newClaimId;
    }
}
