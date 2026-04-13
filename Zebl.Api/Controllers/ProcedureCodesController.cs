using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Models;
using Zebl.Application.Abstractions;
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
    private readonly ICurrentUserContext _userContext;
    private readonly ICurrentContext _currentContext;

    public ProcedureCodesController(
        Context.ZeblDbContext context,
        IProcedureCodeLookupService lookupService,
        IClaimChargeCalculator chargeCalculator,
        INOC837Formatter nocFormatter,
        ICurrentUserContext userContext,
        ICurrentContext currentContext)
    {
        _context = context;
        _lookupService = lookupService;
        _chargeCalculator = chargeCalculator;
        _nocFormatter = nocFormatter;
        _userContext = userContext;
        _currentContext = currentContext;
    }

    private IActionResult? RequireTenantAndFacility()
    {
        if (_userContext.TenantId <= 0)
            return BadRequest(new { message = "A valid tenant is required for procedure codes." });
        if (_currentContext.FacilityId <= 0)
            return BadRequest(new { message = "A valid facility is required for procedure codes." });
        return null;
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
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;
        var query = _context.Procedure_Codes.AsNoTracking().Where(p => p.TenantId == tid && p.FacilityId == fid);

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
    public async Task<IActionResult> Lookup(
        [FromQuery] string? procedureCode,
        [FromQuery] int? payerId,
        [FromQuery] int? billingPhysicianId,
        [FromQuery] string? rateClass,
        [FromQuery] System.DateTime? serviceDate,
        [FromQuery] string? productCode)
    {
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        if (string.IsNullOrWhiteSpace(procedureCode))
            return BadRequest(new { message = "procedureCode is required." });

        var serviceDateValue = serviceDate ?? System.DateTime.UtcNow.Date;
        var best = await _lookupService.LookupAsync(
            _userContext.TenantId,
            _currentContext.FacilityId,
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
    /// GET single procedure code by code (EZClaim service-line lookup).
    /// </summary>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Procedure code is required." });

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;
        var entity = await _context.Procedure_Codes
            .AsNoTracking()
            .Where(p => p.TenantId == tid && p.FacilityId == fid && p.ProcCode == code.Trim())
            .OrderBy(p => p.ProcProductCode)
            .FirstOrDefaultAsync();

        if (entity == null)
            return NotFound();

        return Ok(new
        {
            entity.ProcCode,
            entity.ProcCharge,
            entity.ProcAllowed,
            entity.ProcUnits,
            entity.ProcModifier1,
            entity.ProcModifier2,
            entity.ProcModifier3,
            entity.ProcModifier4,
            entity.ProcDescription
        });
    }

    /// <summary>
    /// GET single procedure code by ID.
    /// </summary>
    [HttpGet("id/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;
        var entity = await _context.Procedure_Codes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProcID == id && p.TenantId == tid && p.FacilityId == fid);

        if (entity == null)
            return NotFound();

        return Ok(entity);
    }

    /// <summary>
    /// POST create a new procedure code.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Procedure_Code model)
    {
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        if (model == null)
            return BadRequest(new { message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(model.ProcCode))
            return BadRequest(new { message = "ProcCode is required." });
        if (model.ProcUnits < 1)
            return BadRequest(new { message = "ProcUnits must be greater than or equal to 1." });

        model.ProcID = 0;
        model.TenantId = _userContext.TenantId;
        model.FacilityId = _currentContext.FacilityId;
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
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        if (model == null)
            return BadRequest(new { message = "Request body is required." });
        if (id != model.ProcID)
            return BadRequest(new { message = "ID in route does not match ProcID in body." });
        if (string.IsNullOrWhiteSpace(model.ProcCode))
            return BadRequest(new { message = "ProcCode is required." });
        if (model.ProcUnits < 1)
            return BadRequest(new { message = "ProcUnits must be greater than or equal to 1." });

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;
        var existing = await _context.Procedure_Codes.FirstOrDefaultAsync(p => p.ProcID == id && p.TenantId == tid && p.FacilityId == fid);
        if (existing == null)
            return NotFound();

        var keepProcId = existing.ProcID;
        var keepTenant = existing.TenantId;
        var keepFacility = existing.FacilityId;
        _context.Entry(existing).CurrentValues.SetValues(model);
        existing.ProcID = keepProcId;
        existing.TenantId = keepTenant;
        existing.FacilityId = keepFacility;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    /// <summary>
    /// DELETE a procedure code by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;
        var entity = await _context.Procedure_Codes.FirstOrDefaultAsync(p => p.ProcID == id && p.TenantId == tid && p.FacilityId == fid);
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
        var bad = RequireTenantAndFacility();
        if (bad != null) return bad;

        var tid = _userContext.TenantId;
        var fid = _currentContext.FacilityId;

        if (items == null)
            return BadRequest(new { message = "Request body is required." });

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProcCode))
                return BadRequest(new { message = "ProcCode is required for all items." });
        }

        var physicianIds = await _context.Physicians
            .AsNoTracking()
            .Where(p => p.TenantId == tid)
            .OrderBy(p => p.PhyID)
            .Select(p => p.PhyID)
            .ToListAsync();
        var validPhysicianIds = new HashSet<int>(physicianIds);
        var defaultBillingPhyId = physicianIds.Count > 0 ? physicianIds[0] : 0;

        var payerIds = await _context.Payers
            .AsNoTracking()
            .Where(p => p.TenantId == tid)
            .Select(p => p.PayID)
            .ToListAsync();
        var validPayerIds = new HashSet<int>(payerIds);

        var now = System.DateTime.UtcNow;
        foreach (var item in items)
        {
            if (item.ProcID == 0)
                item.ProcDateTimeCreated = now;

            item.ProcDateTimeModified = now;

            item.ProcAdjust = item.ProcAdjust;
            item.ProcAllowed = item.ProcAllowed;
            item.ProcCharge = item.ProcCharge;
            item.ProcCost = item.ProcCost;
            item.ProcDrugUnitCount = item.ProcDrugUnitCount;
            item.ProcRVUMalpractice = item.ProcRVUMalpractice;
            item.ProcRVUWork = item.ProcRVUWork;

            if (item.ProcUnits < 1)
                item.ProcUnits = 1;

            // Column is non-null FK — never use a hardcoded PhyID from another tenant/environment.
            if (item.ProcBillingPhyFID == 0)
            {
                if (defaultBillingPhyId == 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "No physicians exist for your tenant. Import or add physicians first, or select a billing physician on each procedure row.",
                        code = "NoPhysiciansForBillingDefault"
                    });
                }

                item.ProcBillingPhyFID = defaultBillingPhyId;
            }

            if (item.ProcPayFID == 0)
                item.ProcPayFID = null;
        }

        foreach (var item in items)
        {
            if (item.ProcBillingPhyFID != 0 && !validPhysicianIds.Contains(item.ProcBillingPhyFID))
            {
                return BadRequest(new
                {
                    message = $"Invalid physician reference ProcBillingPhyFID={item.ProcBillingPhyFID} for ProcCode '{item.ProcCode}'.",
                    code = "InvalidBillingPhysician"
                });
            }

            if (item.ProcPayFID.HasValue && !validPayerIds.Contains(item.ProcPayFID.Value))
            {
                return BadRequest(new
                {
                    message = $"Invalid payer reference ProcPayFID={item.ProcPayFID} for ProcCode '{item.ProcCode}'.",
                    code = "InvalidPayer"
                });
            }
        }

        foreach (var item in items)
        {
            if (item.ProcID == 0)
            {
                item.TenantId = tid;
                item.FacilityId = fid;
                _context.Procedure_Codes.Add(item);
            }
            else
            {
                var existing = await _context.Procedure_Codes.FirstOrDefaultAsync(p => p.ProcID == item.ProcID && p.TenantId == tid && p.FacilityId == fid);
                if (existing == null)
                {
                    return BadRequest(new
                    {
                        message = $"Procedure ProcID {item.ProcID} was not found for your facility.",
                        code = "ProcedureNotFound"
                    });
                }

                var keepProcId = existing.ProcID;
                var keepTenant = existing.TenantId;
                var keepFacility = existing.FacilityId;
                _context.Entry(existing).CurrentValues.SetValues(item);
                existing.ProcID = keepProcId;
                existing.TenantId = keepTenant;
                existing.FacilityId = keepFacility;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return BadRequest(new
            {
                message = "Bulk save failed due to database constraint violation.",
                detail = ex.InnerException?.Message ?? ex.Message
            });
        }

        return Ok(new { success = true });
    }
}
