using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Finds the correct procedure code library entry using lookup priority rules.
/// </summary>
public interface IProcedureCodeLookupService
{
    /// <summary>
    /// Lookup best-matching procedure code by priority: Billing Physician + Payer + Rate Class,
    /// then Payer + Rate Class, then Rate Class only, then generic.
    /// </summary>
    Task<IProcedureCode?> LookupAsync(
        int tenantId,
        int facilityId,
        string procedureCode,
        int? payerId,
        int? billingPhysicianId,
        string? rateClass,
        DateTime serviceDate,
        string? productCode);
}
