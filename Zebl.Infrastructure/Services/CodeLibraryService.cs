using Zebl.Application.Dtos.CodeLibrary;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Entities;
using Zebl.Infrastructure.Repositories;

namespace Zebl.Infrastructure.Services;

public class CodeLibraryService : ICodeLibraryService
{
    private readonly DiagnosisCodeRepository _diagnosisRepo;
    private readonly ModifierCodeRepository _modifierRepo;
    private readonly PlaceOfServiceRepository _posRepo;
    private readonly ReasonCodeRepository _reasonRepo;
    private readonly RemarkCodeRepository _remarkRepo;

    public CodeLibraryService(
        DiagnosisCodeRepository diagnosisRepo,
        ModifierCodeRepository modifierRepo,
        PlaceOfServiceRepository posRepo,
        ReasonCodeRepository reasonRepo,
        RemarkCodeRepository remarkRepo)
    {
        _diagnosisRepo = diagnosisRepo;
        _modifierRepo = modifierRepo;
        _posRepo = posRepo;
        _reasonRepo = reasonRepo;
        _remarkRepo = remarkRepo;
    }

    private static DiagnosisCodeDto ToDto(Diagnosis_Code e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Description = e.Description,
        CodeType = e.CodeType,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static SimpleCodeDto ToSimpleDto(Modifier_Code e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Description = e.Description,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static SimpleCodeDto ToSimpleDto(Place_of_Service e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Description = e.Description,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static SimpleCodeDto ToSimpleDto(Reason_Code e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Description = e.Description,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static SimpleCodeDto ToSimpleDto(Remark_Code e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Description = e.Description,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static CodeLibraryItemDto ToLookupDto(string code, string? desc) => new() { Code = code, Description = desc };

    public async Task<CodeLibraryPagedResult<DiagnosisCodeDto>> GetDiagnosisPagedAsync(int page, int pageSize, string? search, bool activeOnly = true, string? codeType = null)
    {
        var (items, total) = await _diagnosisRepo.GetPagedAsync(page, pageSize, search, activeOnly, codeType);
        return new CodeLibraryPagedResult<DiagnosisCodeDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total
        };
    }

    public async Task<List<CodeLibraryItemDto>> LookupDiagnosisAsync(string keyword, int limit = 100)
    {
        var list = await _diagnosisRepo.LookupAsync(keyword, limit);
        return list.Select(e => ToLookupDto(e.Code, e.Description)).ToList();
    }

    public async Task<DiagnosisCodeDto?> GetDiagnosisByIdAsync(int id)
    {
        var e = await _diagnosisRepo.GetByIdAsync(id);
        return e == null ? null : ToDto(e);
    }

    public async Task<DiagnosisCodeDto> CreateDiagnosisAsync(DiagnosisCodeDto dto)
    {
        var e = new Diagnosis_Code
        {
            Code = dto.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CodeType = (dto.CodeType ?? "ICD10").Trim(),
            IsActive = dto.IsActive
        };
        e = await _diagnosisRepo.AddAsync(e);
        return ToDto(e);
    }

    public async Task UpdateDiagnosisAsync(DiagnosisCodeDto dto)
    {
        var e = await _diagnosisRepo.GetByIdAsync(dto.Id);
        if (e == null) return;
        e.Code = dto.Code.Trim();
        e.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        e.CodeType = (dto.CodeType ?? "ICD10").Trim();
        e.IsActive = dto.IsActive;
        await _diagnosisRepo.UpdateAsync(e);
    }

    public async Task DeleteDiagnosisAsync(int id) => await _diagnosisRepo.DeleteAsync(id);

    public async Task<CodeLibraryPagedResult<SimpleCodeDto>> GetModifiersPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var (items, total) = await _modifierRepo.GetPagedAsync(page, pageSize, search, activeOnly);
        return new CodeLibraryPagedResult<SimpleCodeDto> { Items = items.Select(ToSimpleDto).ToList(), TotalCount = total };
    }

    public async Task<List<CodeLibraryItemDto>> LookupModifiersAsync(string keyword, int limit = 100)
    {
        var list = await _modifierRepo.LookupAsync(keyword, limit);
        return list.Select(e => ToLookupDto(e.Code, e.Description)).ToList();
    }

    public async Task<SimpleCodeDto?> GetModifierByIdAsync(int id)
    {
        var e = await _modifierRepo.GetByIdAsync(id);
        return e == null ? null : ToSimpleDto(e);
    }

    public async Task<SimpleCodeDto> CreateModifierAsync(SimpleCodeDto dto)
    {
        var e = new Modifier_Code { Code = dto.Code.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive };
        e = await _modifierRepo.AddAsync(e);
        return ToSimpleDto(e);
    }

    public async Task UpdateModifierAsync(SimpleCodeDto dto)
    {
        var e = await _modifierRepo.GetByIdAsync(dto.Id);
        if (e == null) return;
        e.Code = dto.Code.Trim();
        e.Description = dto.Description?.Trim();
        e.IsActive = dto.IsActive;
        await _modifierRepo.UpdateAsync(e);
    }

    public async Task DeleteModifierAsync(int id) => await _modifierRepo.DeleteAsync(id);

    public async Task<CodeLibraryPagedResult<SimpleCodeDto>> GetPlaceOfServicePagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var (items, total) = await _posRepo.GetPagedAsync(page, pageSize, search, activeOnly);
        return new CodeLibraryPagedResult<SimpleCodeDto> { Items = items.Select(ToSimpleDto).ToList(), TotalCount = total };
    }

    public async Task<List<CodeLibraryItemDto>> LookupPlaceOfServiceAsync(string keyword, int limit = 100)
    {
        var list = await _posRepo.LookupAsync(keyword, limit);
        return list.Select(e => ToLookupDto(e.Code, e.Description)).ToList();
    }

    public async Task<SimpleCodeDto?> GetPlaceOfServiceByIdAsync(int id)
    {
        var e = await _posRepo.GetByIdAsync(id);
        return e == null ? null : ToSimpleDto(e);
    }

    public async Task<SimpleCodeDto> CreatePlaceOfServiceAsync(SimpleCodeDto dto)
    {
        var e = new Place_of_Service { Code = dto.Code.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive };
        e = await _posRepo.AddAsync(e);
        return ToSimpleDto(e);
    }

    public async Task UpdatePlaceOfServiceAsync(SimpleCodeDto dto)
    {
        var e = await _posRepo.GetByIdAsync(dto.Id);
        if (e == null) return;
        e.Code = dto.Code.Trim();
        e.Description = dto.Description?.Trim();
        e.IsActive = dto.IsActive;
        await _posRepo.UpdateAsync(e);
    }

    public async Task DeletePlaceOfServiceAsync(int id) => await _posRepo.DeleteAsync(id);

    public async Task<CodeLibraryPagedResult<SimpleCodeDto>> GetReasonsPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var (items, total) = await _reasonRepo.GetPagedAsync(page, pageSize, search, activeOnly);
        return new CodeLibraryPagedResult<SimpleCodeDto> { Items = items.Select(ToSimpleDto).ToList(), TotalCount = total };
    }

    public async Task<List<CodeLibraryItemDto>> LookupReasonsAsync(string keyword, int limit = 100)
    {
        var list = await _reasonRepo.LookupAsync(keyword, limit);
        return list.Select(e => ToLookupDto(e.Code, e.Description)).ToList();
    }

    public async Task<SimpleCodeDto?> GetReasonByIdAsync(int id)
    {
        var e = await _reasonRepo.GetByIdAsync(id);
        return e == null ? null : ToSimpleDto(e);
    }

    public async Task<SimpleCodeDto> CreateReasonAsync(SimpleCodeDto dto)
    {
        var e = new Reason_Code { Code = dto.Code.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive };
        e = await _reasonRepo.AddAsync(e);
        return ToSimpleDto(e);
    }

    public async Task UpdateReasonAsync(SimpleCodeDto dto)
    {
        var e = await _reasonRepo.GetByIdAsync(dto.Id);
        if (e == null) return;
        e.Code = dto.Code.Trim();
        e.Description = dto.Description?.Trim();
        e.IsActive = dto.IsActive;
        await _reasonRepo.UpdateAsync(e);
    }

    public async Task DeleteReasonAsync(int id) => await _reasonRepo.DeleteAsync(id);

    public async Task<CodeLibraryPagedResult<SimpleCodeDto>> GetRemarksPagedAsync(int page, int pageSize, string? search, bool activeOnly = true)
    {
        var (items, total) = await _remarkRepo.GetPagedAsync(page, pageSize, search, activeOnly);
        return new CodeLibraryPagedResult<SimpleCodeDto> { Items = items.Select(ToSimpleDto).ToList(), TotalCount = total };
    }

    public async Task<List<CodeLibraryItemDto>> LookupRemarksAsync(string keyword, int limit = 100)
    {
        var list = await _remarkRepo.LookupAsync(keyword, limit);
        return list.Select(e => ToLookupDto(e.Code, e.Description)).ToList();
    }

    public async Task<SimpleCodeDto?> GetRemarkByIdAsync(int id)
    {
        var e = await _remarkRepo.GetByIdAsync(id);
        return e == null ? null : ToSimpleDto(e);
    }

    public async Task<SimpleCodeDto> CreateRemarkAsync(SimpleCodeDto dto)
    {
        var e = new Remark_Code { Code = dto.Code.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive };
        e = await _remarkRepo.AddAsync(e);
        return ToSimpleDto(e);
    }

    public async Task UpdateRemarkAsync(SimpleCodeDto dto)
    {
        var e = await _remarkRepo.GetByIdAsync(dto.Id);
        if (e == null) return;
        e.Code = dto.Code.Trim();
        e.Description = dto.Description?.Trim();
        e.IsActive = dto.IsActive;
        await _remarkRepo.UpdateAsync(e);
    }

    public async Task DeleteRemarkAsync(int id) => await _remarkRepo.DeleteAsync(id);

    public async Task<CodeLibraryImportResult> ImportDiagnosisAsync(Stream csvStream)
    {
        var rows = ParseCodeDescriptionFile(csvStream);
        int imported = 0, skipped = 0;
        string codeType = "ICD10";
        foreach (var (code, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(code)) { skipped++; continue; }
            if (code.Length > 20) { skipped++; continue; } // Diagnosis_Code.Code is varchar(20)
            var existing = await _diagnosisRepo.GetByCodeAsync(code, codeType);
            if (existing != null) { skipped++; continue; }
            await _diagnosisRepo.AddAsync(new Diagnosis_Code
            {
                Code = code,
                Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                CodeType = codeType,
                IsActive = true
            });
            imported++;
        }
        return new CodeLibraryImportResult { ImportedCount = imported, SkippedCount = skipped };
    }

    public async Task<CodeLibraryImportResult> ImportModifiersAsync(Stream csvStream)
    {
        var rows = ParseCodeDescriptionFile(csvStream);
        int imported = 0, skipped = 0;
        foreach (var (code, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(code)) { skipped++; continue; }
            if (code.Length > 10) { skipped++; continue; } // Modifier_Code.Code is varchar(10)
            if (await _modifierRepo.GetByCodeAsync(code) != null) { skipped++; continue; }
            await _modifierRepo.AddAsync(new Modifier_Code { Code = code, Description = string.IsNullOrWhiteSpace(desc) ? null : desc, IsActive = true });
            imported++;
        }
        return new CodeLibraryImportResult { ImportedCount = imported, SkippedCount = skipped };
    }

    public async Task<CodeLibraryImportResult> ImportPlaceOfServiceAsync(Stream csvStream)
    {
        var rows = ParseCodeDescriptionFile(csvStream);
        int imported = 0, skipped = 0;
        foreach (var (code, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(code)) { skipped++; continue; }
            if (code.Length > 10) { skipped++; continue; } // Place_of_Service.Code is varchar(10)
            if (await _posRepo.GetByCodeAsync(code) != null) { skipped++; continue; }
            await _posRepo.AddAsync(new Place_of_Service { Code = code, Description = string.IsNullOrWhiteSpace(desc) ? null : desc, IsActive = true });
            imported++;
        }
        return new CodeLibraryImportResult { ImportedCount = imported, SkippedCount = skipped };
    }

    public async Task<CodeLibraryImportResult> ImportReasonsAsync(Stream csvStream)
    {
        var rows = ParseCodeDescriptionFile(csvStream);
        int imported = 0, skipped = 0;
        foreach (var (code, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(code)) { skipped++; continue; }
            if (code.Length > 10) { skipped++; continue; } // Reason_Code.Code is varchar(10)
            if (await _reasonRepo.GetByCodeAsync(code) != null) { skipped++; continue; }
            await _reasonRepo.AddAsync(new Reason_Code { Code = code, Description = string.IsNullOrWhiteSpace(desc) ? null : desc, IsActive = true });
            imported++;
        }
        return new CodeLibraryImportResult { ImportedCount = imported, SkippedCount = skipped };
    }

    public async Task<CodeLibraryImportResult> ImportRemarksAsync(Stream csvStream)
    {
        var rows = ParseCodeDescriptionFile(csvStream);
        int imported = 0, skipped = 0;
        foreach (var (code, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(code)) { skipped++; continue; }
            if (code.Length > 20) { skipped++; continue; } // Remark_Code.Code is varchar(20)
            if (await _remarkRepo.GetByCodeAsync(code) != null) { skipped++; continue; }
            await _remarkRepo.AddAsync(new Remark_Code { Code = code, Description = string.IsNullOrWhiteSpace(desc) ? null : desc, IsActive = true });
            imported++;
        }
        return new CodeLibraryImportResult { ImportedCount = imported, SkippedCount = skipped };
    }

    private static List<(string Code, string Description)> ParseCodeDescriptionFile(Stream stream)
    {
        var list = new List<(string, string)>();
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Supports either TAB-separated or comma-separated (simple CSV: Code,Description).
            // We intentionally split into 2 columns so descriptions can contain additional delimiters.
            string[] parts;
            if (line.Contains('\t'))
                parts = line.Split('\t', 2, StringSplitOptions.None);
            else
                parts = line.Split(',', 2, StringSplitOptions.None);

            var code = parts.Length > 0 ? parts[0].Trim().Trim('"') : "";
            var desc = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";

            // Skip header row (Code,Description / Code<TAB>Description)
            if (code.Equals("code", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add((code, desc));
        }
        return list;
    }
}
