using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Models;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Entities;
using Context = Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/procedure-codes")]
[Authorize(Policy = "RequireAuth")]
public class ProcedureCodesController : ControllerBase
{
    private readonly Context.ZeblDbContext _context;
    private readonly IProcedureCodeLookupService _lookupService;
    private readonly IClaimChargeCalculator _chargeCalculator;
    private readonly INOC837Formatter _nocFormatter;

    public ProcedureCodesController(
        Context.ZeblDbContext context,
        IProcedureCodeLookupService lookupService,
        IClaimChargeCalculator chargeCalculator,
        INOC837Formatter nocFormatter)
    {
        _context = context;
        _lookupService = lookupService;
        _chargeCalculator = chargeCalculator;
        _nocFormatter = nocFormatter;
    }

    /// <summary>
    /// GET paged list of procedure codes for grid maintenance.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        int page = 1,
        int pageSize = 50,
        [FromQuery] string? code = null,
        [FromQuery] string? category = null,
        [FromQuery] string? subCategory = null)
    {
        var query = _context.Procedure_Codes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(p => p.ProcCode.Contains(code.Trim()));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.ProcCategory == category.Trim());
        if (!string.IsNullOrWhiteSpace(subCategory))
            query = query.Where(p => p.ProcSubCategory == subCategory.Trim());

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.ProcCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    /// <summary>
    /// Lookup best-matching procedure code for claim entry. Uses ProcedureCodeLookupService, then NOC837Formatter for description.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ProcedureCodeLookupResult>> Lookup(
        [FromQuery] string? procedureCode,
        [FromQuery] int? payerId,
        [FromQuery] int? billingPhysicianId,
        [FromQuery] string? rateClass,
        [FromQuery] System.DateTime? serviceDate,
        [FromQuery] string? productCode)
    {
        if (string.IsNullOrWhiteSpace(procedureCode))
            return BadRequest(new { message = "procedureCode is required." });

        var serviceDateValue = serviceDate ?? System.DateTime.UtcNow.Date;
        var best = await _lookupService.LookupAsync(
            procedureCode.Trim(),
            payerId,
            billingPhysicianId,
            rateClass,
            serviceDateValue,
            productCode);

        if (best == null)
            return NotFound(new { message = "No matching procedure code found." });

        var calc = _chargeCalculator.Calculate(best, 1, 0, 0, false);
        var result = new ProcedureCodeLookupResult
        {
            ProcedureCode = best,
            OverwriteCharge = calc.OverwriteCharge,
            OverwriteAllowed = calc.OverwriteAllowed,
            OverwriteAdjustment = calc.OverwriteAdjustment,
            NocDescription = _nocFormatter.FormatDescription(best)
        };

        return Ok(result);
    }

    /// <summary>
    /// Recalculate charge when units change. Uses ClaimChargeCalculator.
    /// </summary>
    [HttpGet("recalculate-charge")]
    public IActionResult RecalculateCharge(
        [FromQuery] decimal charge,
        [FromQuery] int oldUnits,
        [FromQuery] int newUnits)
    {
        var result = _chargeCalculator.RecalculateCharge(charge, oldUnits, newUnits);
        return Ok(new { charge = result });
    }

    /// <summary>
    /// GET single procedure code by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Procedure_Code>> GetById(int id)
    {
        var entity = await _context.Procedure_Codes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProcID == id);

        if (entity == null)
            return NotFound();

        return Ok(entity);
    }

    /// <summary>
    /// POST create a new procedure code.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Procedure_Code>> Create([FromBody] Procedure_Code model)
    {
        if (model == null)
            return BadRequest(new { message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(model.ProcCode))
            return BadRequest(new { message = "ProcCode is required." });
        if (model.ProcUnits < 1)
            return BadRequest(new { message = "ProcUnits must be greater than or equal to 1." });

        model.ProcID = 0;
        _context.Procedure_Codes.Add(model);
        await _context.SaveChangesAsync();
        return StatusCode(201, model);
    }

    /// <summary>
    /// PUT update an existing procedure code.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Procedure_Code model)
    {
        if (model == null)
            return BadRequest(new { message = "Request body is required." });
        if (id != model.ProcID)
            return BadRequest(new { message = "ID in route does not match ProcID in body." });
        if (string.IsNullOrWhiteSpace(model.ProcCode))
            return BadRequest(new { message = "ProcCode is required." });
        if (model.ProcUnits < 1)
            return BadRequest(new { message = "ProcUnits must be greater than or equal to 1." });

        var exists = await _context.Procedure_Codes.AsNoTracking().AnyAsync(p => p.ProcID == id);
        if (!exists)
            return NotFound();

        _context.Entry(model).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return Ok(model);
    }

    /// <summary>
    /// DELETE a procedure code by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Procedure_Codes.FindAsync(id);
        if (entity == null)
            return NotFound();

        _context.Procedure_Codes.Remove(entity);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST bulk save for grid editing.
    /// </summary>
    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] List<Procedure_Code> items)
    {
        if (items == null)
            return BadRequest(new { message = "Request body is required." });

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProcCode))
                return BadRequest(new { message = "ProcCode is required for all items." });
            if (item.ProcUnits < 1)
                return BadRequest(new { message = "ProcUnits must be greater than or equal to 1 for all items." });
        }

        foreach (var item in items)
        {
            if (item.ProcID == 0)
                _context.Procedure_Codes.Add(item);
            else
                _context.Entry(item).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
