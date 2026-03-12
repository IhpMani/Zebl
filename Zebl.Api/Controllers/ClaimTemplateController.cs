using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Dtos.ClaimTemplates;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/claim-templates")]
[Authorize(Policy = "RequireAuth")]
public class ClaimTemplateController : ControllerBase
{
    private readonly IClaimTemplateService _service;

    public ClaimTemplateController(IClaimTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClaimTemplateDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.TemplateName))
            return BadRequest(new { message = "TemplateName is required." });

        var created = await _service.CreateAsync(dto);
        return StatusCode(201, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ClaimTemplateDto dto)
    {
        if (dto == null || id != dto.Id)
            return BadRequest();

        await _service.UpdateAsync(id, dto);
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(new { success = true });
    }
}

