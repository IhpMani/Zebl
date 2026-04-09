using System.Linq;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Persistence.Context;

public partial class ZeblDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
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
