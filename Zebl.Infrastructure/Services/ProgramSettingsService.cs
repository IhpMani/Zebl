using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

public class ProgramSettingsService
{
    private readonly ZeblDbContext _dbContext;

    public ProgramSettingsService(ZeblDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<JsonElement> GetSectionAsync(string section, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new ArgumentException("Section is required.", nameof(section));

        var entity = await _dbContext.ProgramSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Section == section, cancellationToken);

        if (entity == null)
        {
            entity = new ProgramSettings
            {
                Section = section,
                SettingsJson = "{}",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = null
            };

            _dbContext.ProgramSettings.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var json = string.IsNullOrWhiteSpace(entity.SettingsJson) ? "{}" : entity.SettingsJson;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }
    }

    public async Task SaveSectionAsync(string section, JsonElement settings, string? updatedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new ArgumentException("Section is required.", nameof(section));

        var json = JsonSerializer.Serialize(settings);

        var entity = await _dbContext.ProgramSettings
            .FirstOrDefaultAsync(x => x.Section == section, cancellationToken);

        if (entity == null)
        {
            entity = new ProgramSettings
            {
                Section = section
            };

            _dbContext.ProgramSettings.Add(entity);
        }

        entity.SettingsJson = json;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedBy;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public sealed class PatientMissingAccountDto
    {
        public int PatId { get; set; }
        public string? Name { get; set; }
        public string? AccountNumber { get; set; }
    }

    public async Task<IReadOnlyList<PatientMissingAccountDto>> SavePatientSectionAsync(JsonElement settings, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var requireAccountNumbers = false;
        if (settings.ValueKind == JsonValueKind.Object &&
            settings.TryGetProperty("requireAccountNumbers", out var requireAccountProp) &&
            requireAccountProp.ValueKind == JsonValueKind.True)
        {
            requireAccountNumbers = true;
        }

        if (requireAccountNumbers)
        {
            var missing = await _dbContext.Patients
                .AsNoTracking()
                .Where(p => string.IsNullOrEmpty(p.PatAccountNo))
                .Select(p => new PatientMissingAccountDto
                {
                    PatId = p.PatID,
                    Name = p.PatFullNameCC ?? (p.PatLastName ?? string.Empty) + ", " + (p.PatFirstName ?? string.Empty),
                    AccountNumber = p.PatAccountNo
                })
                .ToListAsync(cancellationToken);

            if (missing.Count > 0)
            {
                return missing;
            }
        }

        await SaveSectionAsync("patient", settings, updatedBy, cancellationToken);
        return Array.Empty<PatientMissingAccountDto>();
    }
}

