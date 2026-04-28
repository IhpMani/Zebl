using System.Linq;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Persistence.Context;

public partial class ZeblDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantRules();
        ApplyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantRules();
        ApplyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceTenantRules()
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Entity is ITenantEntity &&
                                 (e.State == EntityState.Added || e.State == EntityState.Modified)))
        {
            var entity = (ITenantEntity)entry.Entity;

            if (entry.State == EntityState.Added && entity.TenantId <= 0)
                throw new InvalidOperationException(
                    $"TenantId must be explicitly set (greater than zero) for {entry.Entity.GetType().Name}.");

            if (entry.State == EntityState.Modified)
            {
                var originalValue = entry.OriginalValues[nameof(ITenantEntity.TenantId)];
                if (originalValue is not int original)
                    throw new InvalidOperationException($"Original TenantId is invalid for {entry.Entity.GetType().Name}");
                if (original != entity.TenantId)
                    throw new InvalidOperationException($"TenantId cannot be changed for {entry.Entity.GetType().Name}");
            }
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Entity is ITenantFacilityEntity &&
                                 (e.State == EntityState.Added || e.State == EntityState.Modified)))
        {
            var entity = (ITenantFacilityEntity)entry.Entity;

            if (entry.State == EntityState.Added && entity.FacilityId <= 0)
                throw new InvalidOperationException(
                    $"FacilityId must be explicitly set (greater than zero) for {entry.Entity.GetType().Name}.");

            if (entry.State == EntityState.Modified)
            {
                var originalValue = entry.OriginalValues[nameof(ITenantFacilityEntity.FacilityId)];
                if (originalValue is not int original)
                    throw new InvalidOperationException($"Original FacilityId is invalid for {entry.Entity.GetType().Name}");
                if (original != entity.FacilityId)
                    throw new InvalidOperationException($"FacilityId cannot be changed for {entry.Entity.GetType().Name}");
            }
        }

        foreach (var entry in ChangeTracker.Entries<EdiReport>()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var er = entry.Entity;
            if (entry.State == EntityState.Added && er.TenantId <= 0)
                throw new InvalidOperationException("TenantId must be explicitly set for EdiReport");

            if (entry.State == EntityState.Modified)
            {
                var originalValue = entry.OriginalValues[nameof(EdiReport.TenantId)];
                if (originalValue is not int original)
                    throw new InvalidOperationException("Original TenantId is invalid for EdiReport");
                if (original != er.TenantId)
                    throw new InvalidOperationException("TenantId cannot be changed for EdiReport");
            }
        }

        foreach (var entry in ChangeTracker.Entries<ClaimPayment>()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var payment = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                if (payment.TenantId <= 0 || payment.FacilityId <= 0)
                    throw new InvalidOperationException("ClaimPayment requires explicit TenantId and FacilityId.");
            }
            else
            {
                var originalTenant = entry.OriginalValues[nameof(ClaimPayment.TenantId)];
                var originalFacility = entry.OriginalValues[nameof(ClaimPayment.FacilityId)];
                if (originalTenant is not int ot || originalFacility is not int of)
                    throw new InvalidOperationException("Original tenant/facility is invalid for ClaimPayment");
                if (payment.TenantId != ot || payment.FacilityId != of)
                    throw new InvalidOperationException("ClaimPayment tenant/facility cannot be changed.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<ClaimCreditBalance>()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var credit = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                if (credit.TenantId <= 0 || credit.FacilityId <= 0)
                    throw new InvalidOperationException("ClaimCreditBalance requires explicit TenantId and FacilityId.");
            }
            else
            {
                var originalTenant = entry.OriginalValues[nameof(ClaimCreditBalance.TenantId)];
                var originalFacility = entry.OriginalValues[nameof(ClaimCreditBalance.FacilityId)];
                if (originalTenant is not int ot || originalFacility is not int of)
                    throw new InvalidOperationException("Original tenant/facility is invalid for ClaimCreditBalance");
                if (credit.TenantId != ot || credit.FacilityId != of)
                    throw new InvalidOperationException("ClaimCreditBalance tenant/facility cannot be changed.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<PaymentBatch>()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var batch = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                if (batch.TenantId <= 0 || batch.FacilityId <= 0)
                    throw new InvalidOperationException("PaymentBatch requires explicit TenantId and FacilityId.");
            }
            else
            {
                var originalTenant = entry.OriginalValues[nameof(PaymentBatch.TenantId)];
                var originalFacility = entry.OriginalValues[nameof(PaymentBatch.FacilityId)];
                if (originalTenant is not int ot || originalFacility is not int of)
                    throw new InvalidOperationException("Original tenant/facility is invalid for PaymentBatch");
                if (batch.TenantId != ot || batch.FacilityId != of)
                    throw new InvalidOperationException("PaymentBatch tenant/facility cannot be changed.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<EligibilityRequest>()
                     .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var request = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                if (request.TenantId <= 0 || request.FacilityId <= 0)
                    throw new InvalidOperationException("EligibilityRequest requires explicit TenantId and FacilityId.");
            }
            else
            {
                var originalTenant = entry.OriginalValues[nameof(EligibilityRequest.TenantId)];
                var originalFacility = entry.OriginalValues[nameof(EligibilityRequest.FacilityId)];
                if (originalTenant is not int ot || originalFacility is not int of)
                    throw new InvalidOperationException("Original tenant/facility is invalid for EligibilityRequest");
                if (request.TenantId != ot || request.FacilityId != of)
                    throw new InvalidOperationException("EligibilityRequest tenant/facility cannot be changed.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<FacilityScope>()
                     .Where(e => e.State == EntityState.Added))
        {
            var fs = entry.Entity;
            if (fs.TenantId <= 0 || fs.FacilityId <= 0)
                throw new InvalidOperationException("FacilityScope requires explicit TenantId and FacilityId greater than zero.");
        }

        foreach (var entry in ChangeTracker.Entries<UserFacility>()
                     .Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.FacilityId <= 0)
                throw new InvalidOperationException("UserFacility requires explicit FacilityId greater than zero.");
        }
    }
}
