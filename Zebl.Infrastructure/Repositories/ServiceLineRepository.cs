using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class ServiceLineRepository : IServiceLineRepository
{
    private readonly ZeblDbContext _context;

    public ServiceLineRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceLineTotals?> GetTotalsByIdAsync(int serviceLineId)
    {
        var s = await _context.Service_Lines.AsNoTracking()
            .Where(x => x.SrvID == serviceLineId)
            .Select(x => new ServiceLineTotals
            {
                SrvID = x.SrvID,
                SrvGUID = x.SrvGUID,
                ClaID = x.SrvClaFID,
                Charges = x.SrvCharges,
                TotalInsAmtPaid = x.SrvTotalInsAmtPaidTRIG,
                TotalPatAmtPaid = x.SrvTotalPatAmtPaidTRIG,
                TotalCOAdj = x.SrvTotalCOAdjTRIG,
                TotalCRAdj = x.SrvTotalCRAdjTRIG,
                TotalOAAdj = x.SrvTotalOAAdjTRIG,
                TotalPIAdj = x.SrvTotalPIAdjTRIG,
                TotalPRAdj = x.SrvTotalPRAdjTRIG
            })
            .FirstOrDefaultAsync();
        return s;
    }

    public async Task<List<ServiceLineTotals>> GetTotalsByClaimIdAsync(int claimId)
    {
        return await _context.Service_Lines.AsNoTracking()
            .Where(x => x.SrvClaFID == claimId)
            .Select(x => new ServiceLineTotals
            {
                SrvID = x.SrvID,
                SrvGUID = x.SrvGUID,
                ClaID = x.SrvClaFID,
                Charges = x.SrvCharges,
                TotalInsAmtPaid = x.SrvTotalInsAmtPaidTRIG,
                TotalPatAmtPaid = x.SrvTotalPatAmtPaidTRIG,
                TotalCOAdj = x.SrvTotalCOAdjTRIG,
                TotalCRAdj = x.SrvTotalCRAdjTRIG,
                TotalOAAdj = x.SrvTotalOAAdjTRIG,
                TotalPIAdj = x.SrvTotalPIAdjTRIG,
                TotalPRAdj = x.SrvTotalPRAdjTRIG
            })
            .ToListAsync();
    }

    public async Task<List<ServiceLineTotals>> GetForAutoApplyAsync(int patientId, int? payerId, bool isPayerSource)
    {
        var query = _context.Service_Lines.AsNoTracking()
            .Where(s => s.SrvClaF != null && s.SrvClaF.ClaPatFID == patientId);
        if (isPayerSource && payerId.HasValue && payerId.Value > 0)
            query = query.Where(s => s.SrvResponsibleParty == payerId.Value);
        var list = await query
            .OrderBy(s => s.SrvClaF!.ClaBillDate)
            .ThenBy(s => s.SrvID)
            .Select(x => new ServiceLineTotals
            {
                SrvID = x.SrvID,
                SrvGUID = x.SrvGUID,
                ClaID = x.SrvClaFID,
                Charges = x.SrvCharges,
                TotalInsAmtPaid = x.SrvTotalInsAmtPaidTRIG,
                TotalPatAmtPaid = x.SrvTotalPatAmtPaidTRIG,
                TotalCOAdj = x.SrvTotalCOAdjTRIG,
                TotalCRAdj = x.SrvTotalCRAdjTRIG,
                TotalOAAdj = x.SrvTotalOAAdjTRIG,
                TotalPIAdj = x.SrvTotalPIAdjTRIG,
                TotalPRAdj = x.SrvTotalPRAdjTRIG
            })
            .ToListAsync();
        return list;
    }

    public async Task<List<PaymentEntryServiceLineDto>> GetPaymentEntryLinesAsync(int patientId, int? payerId, bool isPayerSource)
    {
        var query = from s in _context.Service_Lines.AsNoTracking()
                    join c in _context.Claims.AsNoTracking() on s.SrvClaFID equals c.ClaID
                    join p in _context.Patients.AsNoTracking() on c.ClaPatFID equals p.PatID
                    where c.ClaPatFID == patientId
                    select new { s, c, p };

        if (isPayerSource && payerId.HasValue && payerId.Value > 0)
            query = query.Where(x => x.s.SrvResponsibleParty == payerId.Value);

        var list = await query
            .OrderBy(x => x.c.ClaBillDate)
            .ThenBy(x => x.s.SrvID)
            .Select(x => new
            {
                x.s.SrvID,
                x.s.SrvFromDate,
                x.s.SrvProcedureCode,
                x.s.SrvCharges,
                x.s.SrvResponsibleParty,
                x.s.SrvTotalInsAmtPaidTRIG,
                x.s.SrvTotalPatAmtPaidTRIG,
                x.s.SrvTotalCOAdjTRIG,
                x.s.SrvTotalCRAdjTRIG,
                x.s.SrvTotalOAAdjTRIG,
                x.s.SrvTotalPIAdjTRIG,
                x.s.SrvTotalPRAdjTRIG,
                PatName = x.p.PatFullNameCC ?? (x.p.PatLastName + ", " + x.p.PatFirstName)
            })
            .ToListAsync();

        // EZClaim-style: load ONLY service lines with remaining balance
        decimal TotalAdj(decimal co, decimal cr, decimal oa, decimal pi, decimal pr) => co + cr + oa + pi + pr;
        list = list.Where(x =>
        {
            var applied = isPayerSource ? x.SrvTotalInsAmtPaidTRIG : x.SrvTotalPatAmtPaidTRIG;
            var totalAdj = TotalAdj(x.SrvTotalCOAdjTRIG, x.SrvTotalCRAdjTRIG, x.SrvTotalOAAdjTRIG, x.SrvTotalPIAdjTRIG, x.SrvTotalPRAdjTRIG);
            var balance = x.SrvCharges - applied - totalAdj;
            return balance > 0.001m;
        }).ToList();

        var responsiblePartyIds = list.Where(x => x.SrvResponsibleParty > 0).Select(x => x.SrvResponsibleParty).Distinct().ToList();
        var payerNames = new Dictionary<int, string?>();
        if (responsiblePartyIds.Any())
        {
            var payers = await _context.Payers.AsNoTracking()
                .Where(pay => responsiblePartyIds.Contains(pay.PayID))
                .Select(pay => new { pay.PayID, pay.PayName })
                .ToListAsync();
            foreach (var pay in payers)
                payerNames[pay.PayID] = pay.PayName;
        }

        decimal Applied(int isPayer, decimal insPaid, decimal patPaid) => isPayer != 0 ? insPaid : patPaid;

        return list.Select(x =>
        {
            var applied = Applied(isPayerSource ? 1 : 0, x.SrvTotalInsAmtPaidTRIG, x.SrvTotalPatAmtPaidTRIG);
            var totalAdj = TotalAdj(x.SrvTotalCOAdjTRIG, x.SrvTotalCRAdjTRIG, x.SrvTotalOAAdjTRIG, x.SrvTotalPIAdjTRIG, x.SrvTotalPRAdjTRIG);
            var balance = x.SrvCharges - applied - totalAdj;
            var responsible = x.SrvResponsibleParty == 0 ? "Patient" : (payerNames.TryGetValue(x.SrvResponsibleParty, out var name) ? name : null);
            return new PaymentEntryServiceLineDto
            {
                ServiceLineId = x.SrvID,
                Name = x.PatName?.Trim(),
                Dos = x.SrvFromDate != default ? x.SrvFromDate.ToString("MM/dd/yyyy") : null,
                Proc = x.SrvProcedureCode,
                Charge = x.SrvCharges,
                Responsible = responsible,
                Applied = applied,
                Balance = balance
            };
        }).ToList();
    }

    public async Task<int?> AddInsPaidAsync(int serviceLineId, decimal amount)
    {
        var s = await _context.Service_Lines.FindAsync(serviceLineId);
        if (s == null) return null;
        s.SrvTotalInsAmtPaidTRIG += amount;
        SetServiceLineBalance(s);
        s.SrvDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return s.SrvClaFID;
    }

    public async Task<int?> AddPatPaidAsync(int serviceLineId, decimal amount)
    {
        var s = await _context.Service_Lines.FindAsync(serviceLineId);
        if (s == null) return null;
        s.SrvTotalPatAmtPaidTRIG += amount;
        SetServiceLineBalance(s);
        s.SrvDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return s.SrvClaFID;
    }

    public async Task<int?> AddAdjustmentAmountAsync(int serviceLineId, string groupCode, decimal amount)
    {
        var s = await _context.Service_Lines.FindAsync(serviceLineId);
        if (s == null) return null;
        var gc = groupCode.Trim().ToUpperInvariant();
        if (gc.Length > 2) gc = gc.Substring(0, 2);
        switch (gc)
        {
            case "CO": s.SrvTotalCOAdjTRIG += amount; break;
            case "PR": s.SrvTotalPRAdjTRIG += amount; break;
            case "OA": s.SrvTotalOAAdjTRIG += amount; break;
            case "PI": s.SrvTotalPIAdjTRIG += amount; break;
            case "CR": s.SrvTotalCRAdjTRIG += amount; break;
        }
        SetServiceLineBalance(s);
        s.SrvDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return s.SrvClaFID;
    }

    /// <summary>Recalculate SrvTotalBalanceCC = Charge - Paid - Adjustments (EZClaim equation).</summary>
    private static void SetServiceLineBalance(Service_Line s)
    {
        decimal paid = s.SrvTotalInsAmtPaidTRIG + s.SrvTotalPatAmtPaidTRIG;
        decimal totalAdj = s.SrvTotalCOAdjTRIG + s.SrvTotalCRAdjTRIG + s.SrvTotalOAAdjTRIG + s.SrvTotalPIAdjTRIG + s.SrvTotalPRAdjTRIG;
        s.SrvTotalBalanceCC = s.SrvCharges - paid - totalAdj;
    }

    public async Task<(decimal Charges, decimal AllowedAmt, decimal InsPaid, decimal PatPaid, decimal TotalAdj)?> GetBalanceInfoAsync(int serviceLineId)
    {
        var s = await _context.Service_Lines.AsNoTracking().FirstOrDefaultAsync(x => x.SrvID == serviceLineId);
        if (s == null) return null;
        var totalAdj = s.SrvTotalCOAdjTRIG + s.SrvTotalCRAdjTRIG + s.SrvTotalOAAdjTRIG + s.SrvTotalPIAdjTRIG + s.SrvTotalPRAdjTRIG;
        return (s.SrvCharges, s.SrvAllowedAmt, s.SrvTotalInsAmtPaidTRIG, s.SrvTotalPatAmtPaidTRIG, totalAdj);
    }

    public async Task<int> GetPayerIdForLineAsync(int serviceLineId)
    {
        var s = await _context.Service_Lines.AsNoTracking().FirstOrDefaultAsync(x => x.SrvID == serviceLineId);
        return s?.SrvResponsibleParty ?? 0;
    }
}
