using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Shared rules for claims that may appear on Send Claims / enter a send batch.
/// </summary>
public static class SendableClaimsFilter
{
    /// <summary>
    /// Filters claims that are eligible for electronic batch send:
    /// RTS, claim submission method electronic, bill-to not patient (ClaBillTo != 0),
    /// primary insured (sequence 1) linked to a payer with electronic submission and non-empty external ID.
    /// </summary>
    public static IQueryable<Claim> WhereEligibleForSend(
        this IQueryable<Claim> claims,
        int tenantId,
        int facilityId,
        string readyToSubmitStatusStorage,
        bool showBillToPatientClaims)
    {
        return claims.Where(c =>
            c.TenantId == tenantId &&
            c.FacilityId == facilityId &&
            c.ClaStatus == readyToSubmitStatusStorage &&
            c.ClaSubmissionMethod != null &&
            c.ClaSubmissionMethod.ToLower() == "electronic" &&
            (showBillToPatientClaims || (c.ClaBillTo != null && c.ClaBillTo != 0)) &&
            c.Claim_Insureds.Any(i =>
                i.ClaInsSequence == 1 &&
                i.ClaInsPayFID > 0 &&
                i.ClaInsPayF.PaySubmissionMethod != null &&
                i.ClaInsPayF.PaySubmissionMethod.ToLower() == "electronic" &&
                i.ClaInsPayF.PayExternalID != null &&
                i.ClaInsPayF.PayExternalID != ""));
    }
}
