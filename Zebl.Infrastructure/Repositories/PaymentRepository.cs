using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ZeblDbContext _context;

    public PaymentRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<int> AddAsync(int payId, int patientId, int billingPhysicianId, decimal amount, DateOnly paymentDate, string? ref835 = null)
    {
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PmtAmount = amount,
            PmtPayFID = payId,
            PmtPatFID = patientId,
            PmtBFEPFID = billingPhysicianId,
            PmtDate = paymentDate,
            Pmt835Ref = ref835,
            PmtDateTimeCreated = now,
            PmtDateTimeModified = now,
            PmtDisbursedTRIG = 0,
            PmtChargedPlatformFee = 0
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment.PmtID;
    }

    public async Task<int> CreatePaymentAsync(int? payerId, int patientId, int billingPhysicianId, decimal amount, DateOnly date, string? method, string? reference1, string? reference2, string? note, string? ref835 = null)
    {
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PmtAmount = amount,
            PmtPayFID = payerId,
            PmtPatFID = patientId,
            PmtBFEPFID = billingPhysicianId,
            PmtDate = date,
            PmtMethod = method,
            PmtOtherReference1 = reference1,
            PmtOtherReference2 = reference2,
            PmtNote = note,
            Pmt835Ref = ref835,
            PmtDateTimeCreated = now,
            PmtDateTimeModified = now,
            PmtDisbursedTRIG = 0,
            PmtChargedPlatformFee = 0
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment.PmtID;
    }

    public async Task<(int? PayerId, int PatientId, decimal Amount, decimal Disbursed)?> GetByIdAsync(int paymentId)
    {
        var p = await _context.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.PmtID == paymentId);
        if (p == null) return null;
        return (p.PmtPayFID, p.PmtPatFID, p.PmtAmount, p.PmtDisbursedTRIG);
    }

    public async Task<bool> ExistsDuplicateAsync(decimal amount, string? reference1)
    {
        return await _context.Payments.AsNoTracking()
            .AnyAsync(p => p.PmtAmount == amount && p.PmtOtherReference1 == reference1);
    }

    public async Task SetDisbursedAsync(int paymentId, decimal disbursedAmount)
    {
        var p = await _context.Payments.FindAsync(paymentId);
        if (p == null) return;
        p.PmtDisbursedTRIG = disbursedAmount;
        p.PmtDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int paymentId)
    {
        var p = await _context.Payments.FindAsync(paymentId);
        if (p != null)
        {
            _context.Payments.Remove(p);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PaymentForEditDto?> GetPaymentForEditAsync(int paymentId)
    {
        var p = await _context.Payments.AsNoTracking()
            .Where(x => x.PmtID == paymentId)
            .Select(x => new PaymentForEditDto
            {
                PaymentId = x.PmtID,
                PaymentSource = x.PmtPayFID != null && x.PmtPayFID > 0 ? PaymentSourceKind.Payer : PaymentSourceKind.Patient,
                PayerId = x.PmtPayFID,
                PatientId = x.PmtPatFID,
                Amount = x.PmtAmount,
                Date = x.PmtDate,
                Method = x.PmtMethod,
                Reference1 = x.PmtOtherReference1,
                Reference2 = x.PmtOtherReference2,
                Note = x.PmtNote,
                Remaining = x.PmtRemainingCC
            })
            .FirstOrDefaultAsync();
        return p;
    }

    public async Task<(List<PaymentDto> Payments, bool ClaimFound)> GetPaymentsForClaimAsync(int claimId)
    {
        var patientId = await _context.Claims.AsNoTracking()
            .Where(c => c.ClaID == claimId)
            .Select(c => c.ClaPatFID)
            .FirstOrDefaultAsync();
        if (patientId == 0)
            return (new List<PaymentDto>(), false);

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.PmtPatFID == patientId)
            .OrderByDescending(p => p.PmtDate)
            .Select(p => new PaymentDto
            {
                PmtID = p.PmtID,
                PmtDate = p.PmtDate == default ? (DateTime?)null : p.PmtDate.ToDateTime(TimeOnly.MinValue),
                PmtAmount = p.PmtAmount,
                PmtMethod = p.PmtMethod,
                PmtRemainingCC = p.PmtRemainingCC,
                PmtNote = p.PmtNote
            })
            .Take(200)
            .ToListAsync();
        return (payments, true);
    }

    public async Task<(List<PaymentListItemDto> Data, int TotalCount)> GetPaymentListAsync(int page, int pageSize, int? patientId)
    {
        IQueryable<Payment> query = _context.Payments.AsNoTracking();
        if (patientId.HasValue && patientId.Value > 0)
            query = query.Where(p => p.PmtPatFID == patientId.Value);
        query = query.OrderByDescending(p => p.PmtDateTimeCreated);

        var totalCount = await query.CountAsync();

        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentListItemDto
            {
                PmtID = p.PmtID,
                PmtDateTimeCreated = p.PmtDateTimeCreated,
                PmtDateTimeModified = p.PmtDateTimeModified,
                PmtCreatedUserName = p.PmtCreatedUserName,
                PmtLastUserName = p.PmtLastUserName,
                PmtDate = p.PmtDate,
                PmtAmount = p.PmtAmount,
                PmtRemainingCC = p.PmtRemainingCC,
                PmtChargedPlatformFee = p.PmtChargedPlatformFee,
                PmtMethod = p.PmtMethod,
                PmtNote = p.PmtNote,
                Pmt835Ref = p.Pmt835Ref,
                PmtOtherReference1 = p.PmtOtherReference1,
                PmtPatFID = p.PmtPatFID,
                PmtPayFID = p.PmtPayFID,
                PmtBFEPFID = p.PmtBFEPFID,
                PmtAuthCode = p.PmtAuthCode,
                PmtDisbursedTRIG = p.PmtDisbursedTRIG,
                PmtPayerName = p.PmtPayF != null ? p.PmtPayF.PayName : null,
                PayClassification = p.PmtPayF != null ? p.PmtPayF.PayClassification : null,
                PatAccountNo = p.PmtPatF != null ? p.PmtPatF.PatAccountNo : null,
                PatLastName = p.PmtPatF != null ? p.PmtPatF.PatLastName : null,
                PatFirstName = p.PmtPatF != null ? p.PmtPatF.PatFirstName : null,
                PatFullNameCC = p.PmtPatF != null ? p.PmtPatF.PatFullNameCC : null,
                PatClassification = p.PmtPatF != null ? p.PmtPatF.PatClassification : null,
                AdditionalColumns = new Dictionary<string, object?>()
            })
            .ToListAsync();

        return (data, totalCount);
    }
}
