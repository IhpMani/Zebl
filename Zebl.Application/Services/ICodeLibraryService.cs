using Zebl.Application.Dtos.CodeLibrary;

namespace Zebl.Application.Services;

/// <summary>
/// Code Library: diagnosis, modifiers, place of service, reason, remark.
/// Procedure codes are served by existing Procedure_Code table / ProcedureCodesController.
/// </summary>
public interface ICodeLibraryService
{
    // Diagnosis
    Task<CodeLibraryPagedResult<DiagnosisCodeDto>> GetDiagnosisPagedAsync(int page, int pageSize, string? search, bool activeOnly = true, string? codeType = null);
    Task<List<CodeLibraryItemDto>> LookupDiagnosisAsync(string keyword, int limit = 100);
    Task<DiagnosisCodeDto?> GetDiagnosisByIdAsync(int id);
    Task<DiagnosisCodeDto> CreateDiagnosisAsync(DiagnosisCodeDto dto);
    Task UpdateDiagnosisAsync(DiagnosisCodeDto dto);
    Task DeleteDiagnosisAsync(int id);

    // Modifier
    Task<CodeLibraryPagedResult<SimpleCodeDto>> GetModifiersPagedAsync(int page, int pageSize, string? search, bool activeOnly = true);
    Task<List<CodeLibraryItemDto>> LookupModifiersAsync(string keyword, int limit = 100);
    Task<SimpleCodeDto?> GetModifierByIdAsync(int id);
    Task<SimpleCodeDto> CreateModifierAsync(SimpleCodeDto dto);
    Task UpdateModifierAsync(SimpleCodeDto dto);
    Task DeleteModifierAsync(int id);

    // Place of Service
    Task<CodeLibraryPagedResult<SimpleCodeDto>> GetPlaceOfServicePagedAsync(int page, int pageSize, string? search, bool activeOnly = true);
    Task<List<CodeLibraryItemDto>> LookupPlaceOfServiceAsync(string keyword, int limit = 100);
    Task<SimpleCodeDto?> GetPlaceOfServiceByIdAsync(int id);
    Task<SimpleCodeDto> CreatePlaceOfServiceAsync(SimpleCodeDto dto);
    Task UpdatePlaceOfServiceAsync(SimpleCodeDto dto);
    Task DeletePlaceOfServiceAsync(int id);

    // Reason
    Task<CodeLibraryPagedResult<SimpleCodeDto>> GetReasonsPagedAsync(int page, int pageSize, string? search, bool activeOnly = true);
    Task<List<CodeLibraryItemDto>> LookupReasonsAsync(string keyword, int limit = 100);
    Task<SimpleCodeDto?> GetReasonByIdAsync(int id);
    Task<SimpleCodeDto> CreateReasonAsync(SimpleCodeDto dto);
    Task UpdateReasonAsync(SimpleCodeDto dto);
    Task DeleteReasonAsync(int id);

    // Remark
    Task<CodeLibraryPagedResult<SimpleCodeDto>> GetRemarksPagedAsync(int page, int pageSize, string? search, bool activeOnly = true);
    Task<List<CodeLibraryItemDto>> LookupRemarksAsync(string keyword, int limit = 100);
    Task<SimpleCodeDto?> GetRemarkByIdAsync(int id);
    Task<SimpleCodeDto> CreateRemarkAsync(SimpleCodeDto dto);
    Task UpdateRemarkAsync(SimpleCodeDto dto);
    Task DeleteRemarkAsync(int id);

    // Import
    Task<CodeLibraryImportResult> ImportDiagnosisAsync(Stream csvStream);
    Task<CodeLibraryImportResult> ImportModifiersAsync(Stream csvStream);
    Task<CodeLibraryImportResult> ImportPlaceOfServiceAsync(Stream csvStream);
    Task<CodeLibraryImportResult> ImportReasonsAsync(Stream csvStream);
    Task<CodeLibraryImportResult> ImportRemarksAsync(Stream csvStream);
}
