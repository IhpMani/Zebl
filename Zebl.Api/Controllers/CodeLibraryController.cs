using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.CodeLibrary;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/code-library")]
[Authorize(Policy = "RequireAuth")]
public class CodeLibraryController : ControllerBase
{
    private readonly ZeblDbContext _context;
    private readonly ICodeLibraryService _codeLibraryService;
    private const int MaxLookupRows = 100;

    public CodeLibraryController(ZeblDbContext context, ICodeLibraryService codeLibraryService)
    {
        _context = context;
        _codeLibraryService = codeLibraryService;
    }

    /// <summary>GET procedure codes from existing Procedure_Code table. Supports page, pageSize, search. Search limited to 100 rows.</summary>
    [HttpGet("procedures")]
    public async Task<IActionResult> GetProcedures(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        var query = _context.Procedure_Codes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.ProcCode.Contains(s) || (p.ProcDescription != null && p.ProcDescription.Contains(s)));
        }
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.ProcCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new { p.ProcID, p.ProcCode, p.ProcDescription, p.ProcDateTimeCreated, p.ProcDateTimeModified })
            .ToListAsync();
        return Ok(new { items, totalCount = total });
    }

    [HttpGet("diagnosis")]
    public async Task<IActionResult> GetDiagnosis(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? codeType = null)
    {
        var result = await _codeLibraryService.GetDiagnosisPagedAsync(page, pageSize, search, activeOnly, codeType);
        return Ok(new { items = result.Items, totalCount = result.TotalCount });
    }

    [HttpGet("modifiers")]
    public async Task<IActionResult> GetModifiers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true)
    {
        var result = await _codeLibraryService.GetModifiersPagedAsync(page, pageSize, search, activeOnly);
        return Ok(new { items = result.Items, totalCount = result.TotalCount });
    }

    [HttpGet("pos")]
    public async Task<IActionResult> GetPlaceOfService(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true)
    {
        var result = await _codeLibraryService.GetPlaceOfServicePagedAsync(page, pageSize, search, activeOnly);
        return Ok(new { items = result.Items, totalCount = result.TotalCount });
    }

    [HttpGet("reasons")]
    public async Task<IActionResult> GetReasons(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true)
    {
        var result = await _codeLibraryService.GetReasonsPagedAsync(page, pageSize, search, activeOnly);
        return Ok(new { items = result.Items, totalCount = result.TotalCount });
    }

    [HttpGet("remarks")]
    public async Task<IActionResult> GetRemarks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = true)
    {
        var result = await _codeLibraryService.GetRemarksPagedAsync(page, pageSize, search, activeOnly);
        return Ok(new { items = result.Items, totalCount = result.TotalCount });
    }

    /// <summary>Generic lookup for claims: type=diagnosis|modifier|pos|procedure, q=keyword. Returns up to 100 rows.</summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] string type,
        [FromQuery] string? q = null,
        [FromQuery] string? keyword = null)
    {
        var term = (q ?? keyword ?? "").Trim();
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t))
            return BadRequest(new { message = "type is required (diagnosis, modifier, pos, procedure)." });

        List<CodeLibraryItemDto> list;
        switch (t)
        {
            case "diagnosis":
                list = await _codeLibraryService.LookupDiagnosisAsync(term, MaxLookupRows);
                break;
            case "modifier":
                list = await _codeLibraryService.LookupModifiersAsync(term, MaxLookupRows);
                break;
            case "pos":
                list = await _codeLibraryService.LookupPlaceOfServiceAsync(term, MaxLookupRows);
                break;
            case "procedure":
                list = await LookupProceduresAsync(term, MaxLookupRows);
                break;
            default:
                return BadRequest(new { message = "type must be one of: diagnosis, modifier, pos, procedure." });
        }

        return Ok(list);
    }

    private async Task<List<CodeLibraryItemDto>> LookupProceduresAsync(string keyword, int limit)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<CodeLibraryItemDto>();
        var list = await _context.Procedure_Codes.AsNoTracking()
            .Where(p => p.ProcCode.Contains(keyword) || (p.ProcDescription != null && p.ProcDescription.Contains(keyword)))
            .OrderBy(p => p.ProcCode)
            .Take(limit)
            .Select(p => new CodeLibraryItemDto { Code = p.ProcCode, Description = p.ProcDescription })
            .ToListAsync();
        return list;
    }

    /// <summary>CSV import (Code[TAB]Description). Hidden from Swagger due to IFormFile + [FromForm] doc generation issue.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Import([FromForm] string type, [FromForm] IFormFile? file)
    {
        if (string.IsNullOrWhiteSpace(type))
            return BadRequest(new { message = "type is required (diagnosis, modifier, pos, reason, remark)." });
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "A CSV file is required." });

        var t = type.Trim().ToLowerInvariant();
        CodeLibraryImportResult result;
        await using (var stream = file.OpenReadStream())
        {
            switch (t)
            {
                case "diagnosis":
                    result = await _codeLibraryService.ImportDiagnosisAsync(stream);
                    break;
                case "modifier":
                    result = await _codeLibraryService.ImportModifiersAsync(stream);
                    break;
                case "pos":
                    result = await _codeLibraryService.ImportPlaceOfServiceAsync(stream);
                    break;
                case "reason":
                    result = await _codeLibraryService.ImportReasonsAsync(stream);
                    break;
                case "remark":
                    result = await _codeLibraryService.ImportRemarksAsync(stream);
                    break;
                default:
                    return BadRequest(new { message = "type must be one of: diagnosis, modifier, pos, reason, remark." });
            }
        }

        return Ok(new { importedCount = result.ImportedCount, skippedCount = result.SkippedCount });
    }

    // CRUD for diagnosis
    [HttpGet("diagnosis/{id:int}")]
    public async Task<IActionResult> GetDiagnosisById(int id)
    {
        var item = await _codeLibraryService.GetDiagnosisByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("diagnosis")]
    public async Task<IActionResult> CreateDiagnosis([FromBody] DiagnosisCodeDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Code is required." });
        var created = await _codeLibraryService.CreateDiagnosisAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("diagnosis/{id:int}")]
    public async Task<IActionResult> UpdateDiagnosis(int id, [FromBody] DiagnosisCodeDto dto)
    {
        if (dto == null || id != dto.Id) return BadRequest();
        await _codeLibraryService.UpdateDiagnosisAsync(dto);
        return Ok();
    }

    [HttpDelete("diagnosis/{id:int}")]
    public async Task<IActionResult> DeleteDiagnosis(int id)
    {
        await _codeLibraryService.DeleteDiagnosisAsync(id);
        return Ok(new { success = true });
    }

    // CRUD for modifier, pos, reason, remark (same pattern)
    [HttpGet("modifiers/{id:int}")]
    public async Task<IActionResult> GetModifierById(int id)
    {
        var item = await _codeLibraryService.GetModifierByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("modifiers")]
    public async Task<IActionResult> CreateModifier([FromBody] SimpleCodeDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Code is required." });
        var created = await _codeLibraryService.CreateModifierAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("modifiers/{id:int}")]
    public async Task<IActionResult> UpdateModifier(int id, [FromBody] SimpleCodeDto dto)
    {
        if (dto == null || id != dto.Id) return BadRequest();
        await _codeLibraryService.UpdateModifierAsync(dto);
        return Ok();
    }

    [HttpDelete("modifiers/{id:int}")]
    public async Task<IActionResult> DeleteModifier(int id)
    {
        await _codeLibraryService.DeleteModifierAsync(id);
        return Ok(new { success = true });
    }

    [HttpGet("pos/{id:int}")]
    public async Task<IActionResult> GetPlaceOfServiceById(int id)
    {
        var item = await _codeLibraryService.GetPlaceOfServiceByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("pos")]
    public async Task<IActionResult> CreatePlaceOfService([FromBody] SimpleCodeDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Code is required." });
        var created = await _codeLibraryService.CreatePlaceOfServiceAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("pos/{id:int}")]
    public async Task<IActionResult> UpdatePlaceOfService(int id, [FromBody] SimpleCodeDto dto)
    {
        if (dto == null || id != dto.Id) return BadRequest();
        await _codeLibraryService.UpdatePlaceOfServiceAsync(dto);
        return Ok();
    }

    [HttpDelete("pos/{id:int}")]
    public async Task<IActionResult> DeletePlaceOfService(int id)
    {
        await _codeLibraryService.DeletePlaceOfServiceAsync(id);
        return Ok(new { success = true });
    }

    [HttpGet("reasons/{id:int}")]
    public async Task<IActionResult> GetReasonById(int id)
    {
        var item = await _codeLibraryService.GetReasonByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("reasons")]
    public async Task<IActionResult> CreateReason([FromBody] SimpleCodeDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Code is required." });
        var created = await _codeLibraryService.CreateReasonAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("reasons/{id:int}")]
    public async Task<IActionResult> UpdateReason(int id, [FromBody] SimpleCodeDto dto)
    {
        if (dto == null || id != dto.Id) return BadRequest();
        await _codeLibraryService.UpdateReasonAsync(dto);
        return Ok();
    }

    [HttpDelete("reasons/{id:int}")]
    public async Task<IActionResult> DeleteReason(int id)
    {
        await _codeLibraryService.DeleteReasonAsync(id);
        return Ok(new { success = true });
    }

    [HttpGet("remarks/{id:int}")]
    public async Task<IActionResult> GetRemarkById(int id)
    {
        var item = await _codeLibraryService.GetRemarkByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("remarks")]
    public async Task<IActionResult> CreateRemark([FromBody] SimpleCodeDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { message = "Code is required." });
        var created = await _codeLibraryService.CreateRemarkAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("remarks/{id:int}")]
    public async Task<IActionResult> UpdateRemark(int id, [FromBody] SimpleCodeDto dto)
    {
        if (dto == null || id != dto.Id) return BadRequest();
        await _codeLibraryService.UpdateRemarkAsync(dto);
        return Ok();
    }

    [HttpDelete("remarks/{id:int}")]
    public async Task<IActionResult> DeleteRemark(int id)
    {
        await _codeLibraryService.DeleteRemarkAsync(id);
        return Ok(new { success = true });
    }
}
