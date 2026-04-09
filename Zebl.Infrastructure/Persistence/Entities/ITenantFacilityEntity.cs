namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Entities scoped to both tenant and facility must set both explicitly on insert;
/// neither id may be changed after insert.
/// </summary>
public interface ITenantFacilityEntity : ITenantEntity
{
    int FacilityId { get; set; }
}
