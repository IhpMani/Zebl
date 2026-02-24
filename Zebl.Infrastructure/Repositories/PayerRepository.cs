using Microsoft.EntityFrameworkCore;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using PayerDomain = Zebl.Application.Domain.Payer;
using PayerEntity = Zebl.Infrastructure.Persistence.Entities.Payer;

namespace Zebl.Infrastructure.Repositories;

/// <summary>
/// Repository for Payer. Data access only; uses existing [Payer] table. No business logic.
/// </summary>
public class PayerRepository : IPayerRepository
{
    private readonly ZeblDbContext _context;

    public PayerRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<List<PayerDomain>> GetAllAsync(bool includeInactive = false)
    {
        IQueryable<PayerEntity> query = _context.Payers.AsNoTracking().OrderBy(p => p.PayName);
        if (!includeInactive)
            query = query.Where(p => !p.PayInactive);
        var list = await query.ToListAsync();
        return list.Select(MapToDomain).ToList();
    }

    public async Task<(List<PayerDomain> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, bool includeInactive, string? classificationList = null)
    {
        var query = _context.Payers.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => !p.PayInactive);
        if (!string.IsNullOrWhiteSpace(classificationList))
        {
            var classifications = classificationList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (classifications.Count > 0)
                query = query.Where(p => p.PayClassification != null && classifications.Contains(p.PayClassification));
        }
        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.PayID).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items.Select(MapToDomain).ToList(), totalCount);
    }

    public async Task<PayerDomain?> GetByIdAsync(int id)
    {
        var entity = await _context.Payers.AsNoTracking().FirstOrDefaultAsync(p => p.PayID == id);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<PayerDomain>> GetByMatchingKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new List<PayerDomain>();
        var list = await _context.Payers
            .AsNoTracking()
            .Where(p => p.PayPaymentMatchingKey != null && p.PayPaymentMatchingKey == key)
            .ToListAsync();
        return list.Select(MapToDomain).ToList();
    }

    public async Task<List<PayerDomain>> GetEquivalentPayersByMatchingKeyAsync(string key)
    {
        return await GetByMatchingKeyAsync(key);
    }

    public async Task<List<PayerDomain>> GetByExternalIdAsync(string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return new List<PayerDomain>();
        var list = await _context.Payers
            .AsNoTracking()
            .Where(p => p.PayExternalID != null && p.PayExternalID.Trim() == externalId.Trim())
            .ToListAsync();
        return list.Select(MapToDomain).ToList();
    }

    public async Task<bool> IsInUseAsync(int payId)
    {
        var inClaimInsured = await _context.Claim_Insureds.AnyAsync(c => c.ClaInsPayFID == payId);
        if (inClaimInsured) return true;
        var inPayment = await _context.Payments.AnyAsync(p => p.PmtPayFID == payId);
        if (inPayment) return true;
        var inProcedureCode = await _context.Procedure_Codes.AnyAsync(pr => pr.ProcPayFID == payId);
        return inProcedureCode;
    }

    public async Task<PayerDomain> AddAsync(PayerDomain domain)
    {
        var entity = MapToEntity(domain);
        entity.PayID = 0;
        entity.PayDateTimeCreated = DateTime.UtcNow;
        entity.PayDateTimeModified = DateTime.UtcNow;
        _context.Payers.Add(entity);
        await _context.SaveChangesAsync();
        return MapToDomain(entity);
    }

    public async Task UpdateAsync(PayerDomain domain)
    {
        var entity = await _context.Payers.FindAsync(domain.PayID);
        if (entity == null) return;
        MapToEntity(domain, entity);
        entity.PayDateTimeModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Payers.FindAsync(id);
        if (entity != null)
        {
            _context.Payers.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    private static PayerDomain MapToDomain(PayerEntity e)
    {
        return new PayerDomain
        {
            PayID = e.PayID,
            PayDateTimeCreated = e.PayDateTimeCreated,
            PayDateTimeModified = e.PayDateTimeModified,
            PayCreatedUserGUID = e.PayCreatedUserGUID,
            PayLastUserGUID = e.PayLastUserGUID,
            PayCreatedUserName = e.PayCreatedUserName,
            PayLastUserName = e.PayLastUserName,
            PayCreatedComputerName = e.PayCreatedComputerName,
            PayLastComputerName = e.PayLastComputerName,
            PayName = e.PayName,
            PayExternalID = e.PayExternalID,
            PayAddr1 = e.PayAddr1,
            PayAddr2 = e.PayAddr2,
            PayAlwaysExportSupervisingProvider = e.PayAlwaysExportSupervisingProvider,
            PayBox1 = e.PayBox1,
            PayCity = e.PayCity,
            PayClaimFilingIndicator = e.PayClaimFilingIndicator,
            PayClaimType = e.PayClaimType,
            PayClassification = e.PayClassification,
            PayEligibilityPhyID = e.PayEligibilityPhyID,
            PayEligibilityPayerID = e.PayEligibilityPayerID,
            PayEmail = e.PayEmail,
            PayExportAuthIn2400 = e.PayExportAuthIn2400,
            PayExportBillingTaxonomy = e.PayExportBillingTaxonomy,
            PayExportOtherPayerOfficeNumber2330B = e.PayExportOtherPayerOfficeNumber2330B,
            PayExportOriginalRefIn2330B = e.PayExportOriginalRefIn2330B,
            PayExportPatientAmtDueIn2430 = e.PayExportPatientAmtDueIn2430,
            PayExportPatientForPOS12 = e.PayExportPatientForPOS12,
            PayExportPaymentDateIn2330B = e.PayExportPaymentDateIn2330B,
            PayExportSSN = e.PayExportSSN,
            PayFaxNo = e.PayFaxNo,
            PayFollowUpDays = e.PayFollowUpDays,
            PayForwardsClaims = e.PayForwardsClaims,
            PayICDIndicator = e.PayICDIndicator,
            PayIgnoreRenderingProvider = e.PayIgnoreRenderingProvider,
            PayInactive = e.PayInactive,
            PayInsTypeCode = e.PayInsTypeCode,
            PayNotes = e.PayNotes,
            PayOfficeNumber = e.PayOfficeNumber,
            PayPaymentMatchingKey = e.PayPaymentMatchingKey,
            PayPhoneNo = e.PayPhoneNo,
            PayPrintBox30 = e.PayPrintBox30,
            PayFormatDateBox14And15 = e.PayFormatDateBox14And15,
            PayState = e.PayState,
            PaySubmissionMethod = e.PaySubmissionMethod,
            PaySuppressWhenPrinting = e.PaySuppressWhenPrinting,
            PayTotalUndisbursedPaymentsTRIG = e.PayTotalUndisbursedPaymentsTRIG,
            PayExportTrackedPRAdjs = e.PayExportTrackedPRAdjs,
            PayUseTotalAppliedInBox29 = e.PayUseTotalAppliedInBox29,
            PayWebsite = e.PayWebsite,
            PayZip = e.PayZip
        };
    }

    private static PayerEntity MapToEntity(PayerDomain d, PayerEntity? existing = null)
    {
        var e = existing ?? new PayerEntity();
        e.PayName = d.PayName;
        e.PayExternalID = d.PayExternalID;
        e.PayAddr1 = d.PayAddr1;
        e.PayAddr2 = d.PayAddr2;
        e.PayAlwaysExportSupervisingProvider = d.PayAlwaysExportSupervisingProvider;
        e.PayBox1 = d.PayBox1;
        e.PayCity = d.PayCity;
        e.PayClaimFilingIndicator = d.PayClaimFilingIndicator;
        e.PayClaimType = d.PayClaimType ?? "Professional";
        e.PayClassification = d.PayClassification;
        e.PayEligibilityPhyID = d.PayEligibilityPhyID;
        e.PayEligibilityPayerID = d.PayEligibilityPayerID;
        e.PayEmail = d.PayEmail;
        e.PayExportAuthIn2400 = d.PayExportAuthIn2400;
        e.PayExportBillingTaxonomy = d.PayExportBillingTaxonomy;
        e.PayExportOtherPayerOfficeNumber2330B = d.PayExportOtherPayerOfficeNumber2330B;
        e.PayExportOriginalRefIn2330B = d.PayExportOriginalRefIn2330B;
        e.PayExportPatientAmtDueIn2430 = d.PayExportPatientAmtDueIn2430;
        e.PayExportPatientForPOS12 = d.PayExportPatientForPOS12;
        e.PayExportPaymentDateIn2330B = d.PayExportPaymentDateIn2330B;
        e.PayExportSSN = d.PayExportSSN;
        e.PayFaxNo = d.PayFaxNo;
        e.PayFollowUpDays = d.PayFollowUpDays;
        e.PayForwardsClaims = d.PayForwardsClaims;
        e.PayICDIndicator = d.PayICDIndicator;
        e.PayIgnoreRenderingProvider = d.PayIgnoreRenderingProvider;
        e.PayInactive = d.PayInactive;
        e.PayInsTypeCode = d.PayInsTypeCode;
        e.PayNotes = d.PayNotes;
        e.PayOfficeNumber = d.PayOfficeNumber;
        e.PayPaymentMatchingKey = d.PayPaymentMatchingKey;
        e.PayPhoneNo = d.PayPhoneNo;
        e.PayPrintBox30 = d.PayPrintBox30;
        e.PayFormatDateBox14And15 = d.PayFormatDateBox14And15;
        e.PayState = d.PayState;
        e.PaySubmissionMethod = d.PaySubmissionMethod ?? "Paper";
        e.PaySuppressWhenPrinting = d.PaySuppressWhenPrinting;
        e.PayTotalUndisbursedPaymentsTRIG = d.PayTotalUndisbursedPaymentsTRIG;
        e.PayExportTrackedPRAdjs = d.PayExportTrackedPRAdjs;
        e.PayUseTotalAppliedInBox29 = d.PayUseTotalAppliedInBox29;
        e.PayWebsite = d.PayWebsite;
        e.PayZip = d.PayZip;
        if (existing == null)
        {
            e.PayDateTimeCreated = d.PayDateTimeCreated;
            e.PayDateTimeModified = d.PayDateTimeModified;
        }
        return e;
    }

    private static PayerEntity MapToEntity(PayerDomain d)
    {
        return MapToEntity(d, null);
    }
}
