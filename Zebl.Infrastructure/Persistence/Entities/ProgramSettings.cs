using System;

namespace Zebl.Infrastructure.Persistence.Entities;

public class ProgramSettings
{
    public int Id { get; set; }

    public string Section { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

