using System;

namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// DB-first entity for AppUser table (auth).
/// </summary>
public partial class AppUser
{
    public Guid UserGuid { get; set; }

    /// <summary>Null for platform super-admin accounts (no company).</summary>
    public int? TenantId { get; set; }

    /// <summary>Optional default facility for JWT / UX; null for platform super-admin.</summary>
    public int? FacilityId { get; set; }

    public string UserName { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Platform super admin (onboarding only). Not tenant-scoped for authorization to /api/super-admin.</summary>
    public bool IsSuperAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[]? PasswordHash { get; set; }

    public byte[]? PasswordSalt { get; set; }

    /// <summary>Rotated on password login only; embedded in JWT for single active session enforcement.</summary>
    public string? SessionStamp { get; set; }
}
