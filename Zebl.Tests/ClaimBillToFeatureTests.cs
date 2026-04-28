using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zebl.Api.Services;
using Zebl.Application.Domain;
using Zebl.Infrastructure.Persistence.Entities;
using PayerEntity = Zebl.Infrastructure.Persistence.Entities.Payer;

namespace Zebl.Tests;

public class ClaimBillToFeatureTests
{
    [Fact]
    public void ClaimBillToRules_Resolve_NoInsurance_ForcesPatient()
    {
        var resolved = ClaimBillToRules.Resolve(
            requestedBillTo: 1,
            currentBillTo: 1,
            hasInsurance: false);

        Assert.Equal((int)ClaimBillTo.Patient, resolved);
    }

    [Fact]
    public void ClaimBillToRules_Resolve_WithInsurance_DefaultsPrimary_WhenNull()
    {
        var resolved = ClaimBillToRules.Resolve(
            requestedBillTo: null,
            currentBillTo: null,
            hasInsurance: true);

        Assert.Equal((int)ClaimBillTo.Primary, resolved);
    }

    [Fact]
    public void SendableClaimsFilter_ClaBillToPatient_ExcludedWhenShowBillToPatientClaimsFalse()
    {
        var tenantId = 1;
        var facilityId = 1;
        var rts = ClaimStatusCatalog.ToStorage(ClaimStatus.RTS);

        var payer = new PayerEntity
        {
            PayID = 1,
            TenantId = tenantId,
            FacilityId = facilityId,
            PayName = "MEDICAID",
            PaySubmissionMethod = "Electronic",
            PayExternalID = "EXT1"
        };

        var claim = new Claim
        {
            ClaID = 101,
            TenantId = tenantId,
            FacilityId = facilityId,
            ClaStatus = rts,
            ClaSubmissionMethod = "Electronic",
            ClaBillTo = (int)ClaimBillTo.Patient,
            Claim_Insureds = new List<Claim_Insured>
            {
                new Claim_Insured
                {
                    ClaInsSequence = 1,
                    ClaInsPayFID = 1,
                    ClaInsPayF = payer
                }
            }
        };

        var eligible = new[] { claim }
            .AsQueryable()
            .WhereEligibleForSend(tenantId, facilityId, rts, showBillToPatientClaims: false)
            .ToList();

        Assert.Empty(eligible);
    }

    [Fact]
    public void SendableClaimsFilter_ClaBillToPrimary_IncludedWhenShowBillToPatientClaimsFalse()
    {
        var tenantId = 1;
        var facilityId = 1;
        var rts = ClaimStatusCatalog.ToStorage(ClaimStatus.RTS);

        var payer = new PayerEntity
        {
            PayID = 1,
            TenantId = tenantId,
            FacilityId = facilityId,
            PayName = "MEDICAID",
            PaySubmissionMethod = "Electronic",
            PayExternalID = "EXT1"
        };

        var claim = new Claim
        {
            ClaID = 102,
            TenantId = tenantId,
            FacilityId = facilityId,
            ClaStatus = rts,
            ClaSubmissionMethod = "Electronic",
            ClaBillTo = (int)ClaimBillTo.Primary,
            Claim_Insureds = new List<Claim_Insured>
            {
                new Claim_Insured
                {
                    ClaInsSequence = 1,
                    ClaInsPayFID = 1,
                    ClaInsPayF = payer
                }
            }
        };

        var eligible = new[] { claim }
            .AsQueryable()
            .WhereEligibleForSend(tenantId, facilityId, rts, showBillToPatientClaims: false)
            .ToList();

        Assert.Single(eligible);
        Assert.Equal(102, eligible[0].ClaID);
    }

    [Fact]
    public void SendableClaimsFilter_ClaBillToPatient_IncludedWhenShowBillToPatientClaimsTrue()
    {
        var tenantId = 1;
        var facilityId = 1;
        var rts = ClaimStatusCatalog.ToStorage(ClaimStatus.RTS);

        var payer = new PayerEntity
        {
            PayID = 1,
            TenantId = tenantId,
            FacilityId = facilityId,
            PayName = "MEDICAID",
            PaySubmissionMethod = "Electronic",
            PayExternalID = "EXT1"
        };

        var claim = new Claim
        {
            ClaID = 103,
            TenantId = tenantId,
            FacilityId = facilityId,
            ClaStatus = rts,
            ClaSubmissionMethod = "Electronic",
            ClaBillTo = (int)ClaimBillTo.Patient,
            Claim_Insureds = new List<Claim_Insured>
            {
                new Claim_Insured
                {
                    ClaInsSequence = 1,
                    ClaInsPayFID = 1,
                    ClaInsPayF = payer
                }
            }
        };

        var eligible = new[] { claim }
            .AsQueryable()
            .WhereEligibleForSend(tenantId, facilityId, rts, showBillToPatientClaims: true)
            .ToList();

        Assert.Single(eligible);
        Assert.Equal(103, eligible[0].ClaID);
    }
}

