using System;

namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// DB-first entity for AppUser table (auth).
/// </summary>
public partial class AppUser
{
    public Guid UserGuid { get; set; }

    public string UserName { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[]? PasswordHash { get; set; }

    public byte[]? PasswordSalt { get; set; }
}
