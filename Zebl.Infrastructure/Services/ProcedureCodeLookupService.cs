using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Finds the correct procedure code library entry using lookup priority rules (computed in SQL).
/// </summary>
public class ProcedureCodeLookupService : IProcedureCodeLookupService
{
    private readonly ZeblDbContext _context;

    public ProcedureCodeLookupService(ZeblDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IProcedureCode?> LookupAsync(
        string procedureCode,
        int? payerId,
        int? billingPhysicianId,
        string? rateClass,
        DateTime serviceDate,
        string? productCode)
    {
        var code = procedureCode?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
            return null;

        var sd = DateOnly.FromDateTime(serviceDate);
        var rateClassTrimmed = string.IsNullOrWhiteSpace(rateClass) ? null : rateClass.Trim();

        var query = _context.Procedure_Codes
            .AsNoTracking()
            .Where(p => p.ProcCode == code)
            .Where(p =>
                (p.ProcStart == null || p.ProcStart <= sd) &&
                (p.ProcEnd == null || p.ProcEnd >= sd));

        if (!string.IsNullOrWhiteSpace(productCode))
            query = query.Where(p => p.ProcProductCode == productCode.Trim());
        else
            query = query.Where(p => p.ProcProductCode == null || p.ProcProductCode == "");

        var result = await query
            .Select(p => new
            {
                Code = p,
                Priority =
                    (billingPhysicianId.HasValue && p.ProcBillingPhyFID == billingPhysicianId.Value ? 8 : 0) +
                    (payerId.HasValue && p.ProcPayFID == payerId.Value ? 4 : 0) +
                    (rateClassTrimmed != null && p.ProcRateClass == rateClassTrimmed ? 2 : 0)
            })
            .OrderByDescending(x => x.Priority)
            .Select(x => x.Code)
            .FirstOrDefaultAsync();

        return result;
    }
}
