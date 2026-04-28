using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ZeblDbContext _context;
    private readonly ICurrentContext _currentContext;
    private readonly ICurrentUserContext _currentUserContext;

    public PaymentRepository(
        ZeblDbContext context,
        ICurrentContext currentContext,
        ICurrentUserContext currentUserContext)
    {
        _context = context;
        _currentContext = currentContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<int> AddAsync(int payId, int patientId, int? billingPhysicianId, decimal amount, DateOnly paymentDate, string? ref835 = null)
    {
        _ = GetRequiredTenantId();
        var patient = await GetScopedPatientAsync(patientId);
        var validPhysicianId = await ValidateBillingPhysicianIdAsync(billingPhysicianId);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            TenantId = patient.TenantId,
            FacilityId = patient.FacilityId,
            PmtAmount = amount,
            PmtPayFID = payId,
            PmtPatFID = patientId,
            PmtBFEPFID = validPhysicianId ?? throw new ValidationException("Invalid billing physician."),
            PmtDate = paymentDate,
            Pmt835Ref = ref835,
            PmtDateTimeCreated = now,
            PmtDateTimeModified = now,
            PmtDisbursedTRIG = 0,
            PmtChargedPlatformFee = 0
        };
        if (payment.TenantId != patient.TenantId)
            throw new ValidationException("Tenant mismatch: Payment.TenantId must match Patient.TenantId.");
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment.PmtID;
    }

    public async Task<int> CreatePaymentAsync(int? payerId, int patientId, int? billingPhysicianId, decimal amount, DateOnly date, string? method, string? reference1, string? reference2, string? note, string? ref835 = null)
    {
        _ = GetRequiredTenantId();
        var patient = await GetScopedPatientAsync(patientId);
        var validPhysicianId = await ValidateBillingPhysicianIdAsync(billingPhysicianId);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            TenantId = patient.TenantId,
            FacilityId = patient.FacilityId,
            PmtAmount = amount,
            PmtPayFID = payerId,
            PmtPatFID = patientId,
            PmtBFEPFID = validPhysicianId ?? throw new ValidationException("Invalid billing physician."),
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
        if (payment.TenantId != patient.TenantId)
            throw new ValidationException("Tenant mismatch: Payment.TenantId must match Patient.TenantId.");
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment.PmtID;
    }

    private async Task<int?> ValidateBillingPhysicianIdAsync(int? billingPhysicianId)
    {
        var physicianId = billingPhysicianId == 0 ? null : billingPhysicianId;
        if (!physicianId.HasValue)
            return null;

        var tenantId = _currentContext.TenantId;
        var facilityId = _currentContext.FacilityId;
        var exists = await _context.Physicians
            .AsNoTracking()
            .AnyAsync(p =>
                p.PhyID == physicianId.Value &&
                p.TenantId == tenantId &&
                p.FacilityId == facilityId);
        if (!exists)
            throw new ValidationException("Invalid billing physician.");

        return physicianId;
    }

    public async Task<(int? PayerId, int PatientId, decimal Amount, decimal Disbursed)?> GetByIdAsync(int paymentId)
    {
        var fid = _currentContext.FacilityId;
        var p = await _context.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PmtID == paymentId && x.FacilityId == fid);
        if (p == null) return null;
        return (p.PmtPayFID, p.PmtPatFID, p.PmtAmount, p.PmtDisbursedTRIG);
    }

    public async Task<bool> ExistsDuplicateAsync(decimal amount, string? reference1)
    {
        var fid = _currentContext.FacilityId;
        return await _context.Payments.AsNoTracking()
            .AnyAsync(p =>
                p.PmtAmount == amount &&
                p.PmtOtherReference1 == reference1 &&
                p.FacilityId == fid);
    }

    public async Task SetDisbursedAsync(int paymentId, decimal disbursedAmount)
    {
        if (_currentContext.TenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var p = await _context.Payments
            .FirstOrDefaultAsync(x => x.PmtID == paymentId && x.FacilityId == fid);
        if (p == null) return;
        p.PmtDisbursedTRIG = disbursedAmount;
        p.PmtDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int paymentId)
    {
        if (_currentContext.TenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var fid = _currentContext.FacilityId;
        var p = await _context.Payments
            .FirstOrDefaultAsync(x => x.PmtID == paymentId && x.FacilityId == fid);
        if (p != null)
        {
            _context.Payments.Remove(p);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PaymentForEditDto?> GetPaymentForEditAsync(int paymentId)
    {
        var fid = _currentContext.FacilityId;
        var p = await _context.Payments.AsNoTracking()
            .Where(x => x.PmtID == paymentId && x.FacilityId == fid)
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
                Remaining = x.PmtAmount - x.PmtDisbursedTRIG
            })
            .FirstOrDefaultAsync();
        if (p == null)
            return null;

        p.ClaimId = await (
            from d in _context.Disbursements.AsNoTracking()
            join s in _context.Service_Lines.AsNoTracking() on d.DisbSrvFID equals s.SrvID
            where d.DisbPmtFID == paymentId && s.FacilityId == fid
            select (int?)s.SrvClaFID
        ).FirstOrDefaultAsync();

        return p;
    }

    public async Task<(List<PaymentDto> Payments, bool ClaimFound)> GetPaymentsForClaimAsync(int claimId)
    {
        var fid = _currentContext.FacilityId;
        var patientId = await _context.Claims.AsNoTracking()
            .Where(c => c.ClaID == claimId && c.FacilityId == fid)
            .Select(c => c.ClaPatFID)
            .FirstOrDefaultAsync();
        if (patientId == 0)
            return (new List<PaymentDto>(), false);

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.PmtPatFID == patientId && p.FacilityId == fid)
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
        var fid = _currentContext.FacilityId;
        IQueryable<Payment> query = _context.Payments.AsNoTracking()
            .Where(p => p.FacilityId == fid);
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
                CreatedDate = p.PmtDateTimeCreated,
                ModifiedDate = p.PmtDateTimeModified,
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

    public async Task<(List<ClaimPaymentLedgerItemDto> Data, int TotalCount)> GetClaimPaymentLedgerAsync(
        int page,
        int pageSize,
        bool? isApplied,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        string? payer,
        string? claimExternalId)
    {
        var fid = _currentContext.FacilityId;
        var query = _context.ClaimPayments
            .AsNoTracking()
            .Where(p => p.FacilityId == fid);

        if (isApplied.HasValue)
            query = query.Where(p => p.IsApplied == isApplied.Value);
        if (fromDateUtc.HasValue)
            query = query.Where(p => p.PaymentDateUtc >= fromDateUtc.Value);
        if (toDateUtc.HasValue)
            query = query.Where(p => p.PaymentDateUtc <= toDateUtc.Value);

        var normalizedPayer = payer?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPayer))
            query = query.Where(p => p.PayerId != null && p.PayerId.Contains(normalizedPayer));

        var normalizedClaim = claimExternalId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedClaim))
            query = query.Where(p => p.ClaimExternalId.Contains(normalizedClaim));

        query = query.OrderByDescending(p => p.PaymentDateUtc).ThenByDescending(p => p.Id);
        var totalCount = await query.CountAsync();

        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ClaimPaymentLedgerItemDto
            {
                Id = p.Id,
                ClaimId = p.ClaimId,
                ClaimExternalId = p.ClaimExternalId,
                TraceNumber = p.TraceNumber,
                PayerId = p.PayerId,
                PayerLevel = p.PayerLevel,
                PaidAmount = p.PaidAmount,
                AdjustmentAmount = p.AdjustmentAmount,
                PatientResponsibility = p.PatientResponsibility,
                IsApplied = p.IsApplied,
                PaymentDateUtc = p.PaymentDateUtc
            })
            .ToListAsync();

        return (data, totalCount);
    }

    private async Task<Patient> GetScopedPatientAsync(int patientId)
    {
        var fid = _currentContext.FacilityId;
        var patient = await _context.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatID == patientId && p.FacilityId == fid);
        if (patient == null)
            throw new ValidationException("Patient not found in current tenant/facility scope.");
        return patient;
    }

    private int GetRequiredTenantId()
    {
        var tenantId = _currentContext.TenantId;
        if (tenantId <= 0)
            throw new UnauthorizedAccessException("Tenant context is required.");
        return tenantId;
    }
}
