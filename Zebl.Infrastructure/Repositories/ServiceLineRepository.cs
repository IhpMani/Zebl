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
                x.s.SrvTotalAmtAppliedCC,
                x.s.SrvTotalBalanceCC,
                x.s.SrvTotalCOAdjTRIG,
                x.s.SrvTotalCRAdjTRIG,
                x.s.SrvTotalOAAdjTRIG,
                x.s.SrvTotalPIAdjTRIG,
                x.s.SrvTotalPRAdjTRIG,
                PatName = x.p.PatFullNameCC ?? (x.p.PatLastName + ", " + x.p.PatFirstName)
            })
            .ToListAsync();

        // EZClaim-style: load ONLY service lines with remaining balance (prefer persisted SrvTotalBalanceCC).
        decimal TotalAdj(decimal co, decimal cr, decimal oa, decimal pi, decimal pr) => co + cr + oa + pi + pr;
        list = list.Where(x =>
        {
            var totalAdj = TotalAdj(x.SrvTotalCOAdjTRIG, x.SrvTotalCRAdjTRIG, x.SrvTotalOAAdjTRIG, x.SrvTotalPIAdjTRIG, x.SrvTotalPRAdjTRIG);
            var appliedTotal = x.SrvTotalAmtAppliedCC ?? (x.SrvTotalInsAmtPaidTRIG + x.SrvTotalPatAmtPaidTRIG);
            var balance = x.SrvTotalBalanceCC ?? (x.SrvCharges - appliedTotal - totalAdj);
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

        return list.Select(x =>
        {
            var totalAdj = TotalAdj(x.SrvTotalCOAdjTRIG, x.SrvTotalCRAdjTRIG, x.SrvTotalOAAdjTRIG, x.SrvTotalPIAdjTRIG, x.SrvTotalPRAdjTRIG);
            var appliedCc = x.SrvTotalAmtAppliedCC ?? (x.SrvTotalInsAmtPaidTRIG + x.SrvTotalPatAmtPaidTRIG);
            var balance = x.SrvTotalBalanceCC ?? (x.SrvCharges - appliedCc - totalAdj);
            var responsible = x.SrvResponsibleParty == 0 ? "Patient" : (payerNames.TryGetValue(x.SrvResponsibleParty, out var name) ? name : null);
            return new PaymentEntryServiceLineDto
            {
                ServiceLineId = x.SrvID,
                Name = x.PatName?.Trim(),
                Dos = x.SrvFromDate != default ? x.SrvFromDate.ToString("MM/dd/yyyy") : null,
                Proc = x.SrvProcedureCode,
                Charge = x.SrvCharges,
                Responsible = responsible,
                Applied = appliedCc,
                Balance = balance
            };
        }).ToList();
    }

    public async Task<int?> AddInsPaidAsync(int serviceLineId, decimal amount)
    {
        // Incremental math removed to avoid drift; recompute from source tables.
        return await RecalculateServiceLineAsync(serviceLineId);
    }

    public async Task<int?> AddPatPaidAsync(int serviceLineId, decimal amount)
    {
        // Incremental math removed to avoid drift; recompute from source tables.
        return await RecalculateServiceLineAsync(serviceLineId);
    }

    public async Task<int?> AddAdjustmentAmountAsync(int serviceLineId, string groupCode, decimal amount)
    {
        // Incremental math removed to avoid drift; recompute from source tables.
        return await RecalculateServiceLineAsync(serviceLineId);
    }

    public async Task<int?> RecalculateServiceLineAsync(int serviceLineId)
    {
        return await RecalculateServiceLineFinancials(serviceLineId);
    }

    private async Task<int?> RecalculateServiceLineFinancials(int srvId)
    {
        var s = await _context.Service_Lines.FindAsync(srvId);
        if (s == null) return null;

        var disbursements = await (
            from d in _context.Disbursements.AsNoTracking()
            join p in _context.Payments.AsNoTracking() on d.DisbPmtFID equals p.PmtID
            where d.DisbSrvFID == srvId
            select new
            {
                d.DisbAmount,
                IsPayerPayment = p.PmtPayFID.HasValue && p.PmtPayFID.Value > 0
            })
            .ToListAsync();

        var insPaid = disbursements
            .Where(x => x.IsPayerPayment)
            .Sum(x => x.DisbAmount);
        var patPaid = disbursements
            .Where(x => !x.IsPayerPayment)
            .Sum(x => x.DisbAmount);

        var adjustments = await _context.Adjustments.AsNoTracking()
            .Where(a => a.AdjSrvFID == srvId)
            .Select(a => new { a.AdjGroupCode, a.AdjAmount })
            .ToListAsync();

        decimal SumByGroup(string gc) => adjustments
            .Where(a => (a.AdjGroupCode ?? string.Empty).Trim().ToUpperInvariant() == gc)
            .Sum(a => a.AdjAmount);

        s.SrvTotalInsAmtPaidTRIG = insPaid;
        s.SrvTotalPatAmtPaidTRIG = patPaid;
        s.SrvTotalCOAdjTRIG = SumByGroup("CO");
        s.SrvTotalCRAdjTRIG = SumByGroup("CR");
        s.SrvTotalOAAdjTRIG = SumByGroup("OA");
        s.SrvTotalPIAdjTRIG = SumByGroup("PI");
        s.SrvTotalPRAdjTRIG = SumByGroup("PR");

        var totalAdj = s.SrvTotalCOAdjTRIG + s.SrvTotalCRAdjTRIG + s.SrvTotalOAAdjTRIG + s.SrvTotalPIAdjTRIG + s.SrvTotalPRAdjTRIG;
        var balance = s.SrvCharges - s.SrvTotalInsAmtPaidTRIG - s.SrvTotalPatAmtPaidTRIG - totalAdj;
        var patBalance = s.SrvTotalPatAmtPaidTRIG > 0 ? 0m : balance;
        var insBalance = Math.Max(balance - patBalance, 0m);

        // Centralized financial totals: never leave these as NULL.
        s.SrvTotalAdjCC = totalAdj;
        s.SrvTotalAmtPaidCC = s.SrvTotalInsAmtPaidTRIG + s.SrvTotalPatAmtPaidTRIG;
        s.SrvTotalAmtAppliedCC = s.SrvTotalAmtPaidCC + totalAdj;
        s.SrvTotalBalanceCC = balance;
        s.SrvTotalPatBalanceCC = patBalance;
        s.SrvTotalInsBalanceCC = insBalance;
        s.SrvDateTimeModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return s.SrvClaFID;
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

    public async Task AdvanceResponsiblePartyAsync(int serviceLineId)
    {
        var s = await _context.Service_Lines.FindAsync(serviceLineId);
        if (s == null || !s.SrvClaFID.HasValue) return;

        // Determine claim-level primary/secondary payers from insured sequence.
        var insureds = await _context.Claim_Insureds.AsNoTracking()
            .Where(ci => ci.ClaInsClaFID == s.SrvClaFID.Value && ci.ClaInsSequence.HasValue && (ci.ClaInsSequence == 1 || ci.ClaInsSequence == 2))
            .Select(ci => new { ci.ClaInsSequence, ci.ClaInsPayFID })
            .ToListAsync();

        var primaryPayerId = insureds.FirstOrDefault(x => x.ClaInsSequence == 1)?.ClaInsPayFID ?? 0;
        var secondaryPayerId = insureds.FirstOrDefault(x => x.ClaInsSequence == 2)?.ClaInsPayFID ?? 0;

        var current = s.SrvResponsibleParty;
        var next = current;

        if (current > 0 && current == primaryPayerId)
        {
            next = secondaryPayerId > 0 ? secondaryPayerId : 0;
        }
        else if (current > 0 && current == secondaryPayerId)
        {
            next = 0;
        }

        if (next != current)
        {
            s.SrvResponsibleParty = next;
            s.SrvRespChangeDate = DateTime.UtcNow;
            s.SrvDateTimeModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
