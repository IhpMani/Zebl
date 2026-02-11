using System;
using System.Collections.Generic;

namespace Zebl.Api.Models;

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
