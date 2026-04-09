namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Maps an AppUser (<see cref="AppUser.UserGuid"/>) to an allowed facility.
/// </summary>
public class UserFacility
{
    /// <summary>Same as <see cref="AppUser.UserGuid"/> (JWT subject).</summary>
    public Guid UserId { get; set; }

    public int FacilityId { get; set; }
}
