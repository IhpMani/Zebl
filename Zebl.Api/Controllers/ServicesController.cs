using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Services;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/services")]
    [Authorize(Policy = "RequireAuth")]
    public class ServicesController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ICurrentContext _currentContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ILogger<ServicesController> _logger;
        private readonly IProcedureCodeLookupService _procedureLookupService;
        private readonly IClaimChargeCalculator _claimChargeCalculator;
        private readonly IServiceLineRepository _serviceLineRepo;

        public ServicesController(
            ZeblDbContext db,
            ICurrentContext currentContext,
            ICurrentUserContext currentUserContext,
            ILogger<ServicesController> logger,
            IProcedureCodeLookupService procedureLookupService,
            IClaimChargeCalculator claimChargeCalculator,
            IServiceLineRepository serviceLineRepo)
        {
            _db = db;
            _currentContext = currentContext;
            _currentUserContext = currentUserContext;
            _logger = logger;
            _procedureLookupService = procedureLookupService;
            _claimChargeCalculator = claimChargeCalculator;
            _serviceLineRepo = serviceLineRepo;
        }

        // =========================================================
        // 🔴 CLAIM DETAILS — THIS IS THE ONLY ENDPOINT
        // Claim Details MUST call ONLY this
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetServicesForClaim(int claId)
        {
            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            var claimOk = await _db.Claims.AsNoTracking()
                .AnyAsync(c => c.ClaID == claId && c.TenantId == tid && c.FacilityId == fid);
            if (!claimOk)
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Claim not found." });

            var services = await _db.Service_Lines
                .AsNoTracking()
                .Where(s => s.SrvClaFID == claId) // 🔴 CRITICAL FILTER
                .OrderBy(s => s.SrvFromDate)
                .Select(s => new ServiceListItemDto
                {
                    SrvID = s.SrvID,
                    SrvClaFID = s.SrvClaFID,
                    SrvFromDate = s.SrvFromDate,
                    SrvToDate = s.SrvToDate,
                    SrvProcedureCode = s.SrvProcedureCode,
                    SrvModifier1 = s.SrvModifier1,
                    SrvModifier2 = s.SrvModifier2,
                    SrvModifier3 = s.SrvModifier3,
                    SrvModifier4 = s.SrvModifier4,
                    SrvDesc = s.SrvDesc,
                    SrvCharges = s.SrvCharges,
                    SrvAllowedAmt = s.SrvAllowedAmt,
                    SrvUnits = s.SrvUnits,
                    SrvTotalInsAmtPaidTRIG = s.SrvTotalInsAmtPaidTRIG,
                    SrvTotalPatAmtPaidTRIG = s.SrvTotalPatAmtPaidTRIG,
                    SrvTotalAmtAppliedCC = s.SrvTotalAmtAppliedCC,
                    SrvTotalBalanceCC = s.SrvTotalBalanceCC,
                    SrvTotalAmtPaidCC = s.SrvTotalAmtPaidCC,
                    SrvResponsibleParty = s.SrvResponsibleParty,
                    SrvNationalDrugCode = s.SrvNationalDrugCode,
                    SrvDrugUnitCount = s.SrvDrugUnitCount,
                    SrvDrugUnitMeasurement = s.SrvDrugUnitMeasurement,
                    SrvPrescriptionNumber = s.SrvPrescriptionNumber,
                    SrvRevenueCode = s.SrvRevenueCode,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .Take(200) // 🔴 HARD SAFETY LIMIT
                .ToListAsync();

            return Ok(new ApiResponse<List<ServiceListItemDto>>
            {
                Data = services
            });
        }

        // =========================================================
        // 🟡 GLOBAL SERVICES SEARCH (NOT USED IN CLAIM DETAILS)
        // Keep this ONLY for Find → Services
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetServices(
            int page = 1,
            int pageSize = 25,
            int? claimId = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values"
                });
            }

            var requestedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                foreach (var k in additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = k.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) requestedColumns.Add(trimmed);
                }
            }

            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Service"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            var hasClaStatus = columnsToInclude.Any(c => c.Key == "claStatus");
            var hasClaDateTimeCreated = columnsToInclude.Any(c => c.Key == "claDateTimeCreated");
            var hasPatFirstName = columnsToInclude.Any(c => c.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(c => c.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(c => c.Key == "patFullNameCC");

            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            var query = _db.Service_Lines.AsNoTracking()
                .Where(s => s.SrvClaF != null && s.SrvClaF.TenantId == tid && s.SrvClaF.FacilityId == fid);

            if (claimId.HasValue)
            {
                query = query.Where(s => s.SrvClaFID == claimId.Value);
            }

            query = query.OrderByDescending(s => s.SrvID);

            var totalCount = await query.CountAsync();

            List<ServiceListItemDto> data;
            if (columnsToInclude.Count > 0 && (hasClaStatus || hasClaDateTimeCreated || hasPatFirstName || hasPatLastName || hasPatFullNameCC))
            {
                var raw = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        s.SrvID,
                        s.SrvClaFID,
                        s.SrvFromDate,
                        s.SrvToDate,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvResponsibleParty,
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        ClaStatus = s.SrvClaF != null ? s.SrvClaF.ClaStatus : null,
                        ClaDateTimeCreated = s.SrvClaF != null ? s.SrvClaF.ClaDateTimeCreated : default,
                        PatFirstName = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatFirstName : null,
                        PatLastName = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatLastName : null,
                        PatFullNameCC = s.SrvClaF != null && s.SrvClaF.ClaPatF != null ? s.SrvClaF.ClaPatF.PatFullNameCC : null
                    })
                    .ToListAsync();

                data = raw.Select(r =>
                {
                    var addCols = new Dictionary<string, object?>();
                    if (hasClaStatus) addCols["claStatus"] = r.ClaStatus;
                    if (hasClaDateTimeCreated) addCols["claDateTimeCreated"] = r.ClaDateTimeCreated;
                    if (hasPatFirstName) addCols["patFirstName"] = r.PatFirstName;
                    if (hasPatLastName) addCols["patLastName"] = r.PatLastName;
                    if (hasPatFullNameCC) addCols["patFullNameCC"] = r.PatFullNameCC;
                    return new ServiceListItemDto
                    {
                        SrvID = r.SrvID,
                        SrvClaFID = r.SrvClaFID,
                        SrvFromDate = r.SrvFromDate,
                        SrvToDate = r.SrvToDate,
                        SrvProcedureCode = r.SrvProcedureCode,
                        SrvModifier1 = null,
                        SrvModifier2 = null,
                        SrvModifier3 = null,
                        SrvModifier4 = null,
                        SrvDesc = r.SrvDesc,
                        SrvCharges = r.SrvCharges,
                        SrvAllowedAmt = 0,
                        SrvUnits = r.SrvUnits,
                        SrvTotalInsAmtPaidTRIG = 0,
                        SrvTotalPatAmtPaidTRIG = 0,
                        SrvTotalBalanceCC = r.SrvTotalBalanceCC,
                        SrvTotalAmtPaidCC = r.SrvTotalAmtPaidCC,
                        SrvResponsibleParty = r.SrvResponsibleParty,
                        SrvNationalDrugCode = null,
                        SrvDrugUnitCount = null,
                        SrvDrugUnitMeasurement = null,
                        SrvPrescriptionNumber = null,
                        SrvRevenueCode = null,
                        AdditionalColumns = addCols
                    };
                }).ToList();
            }
            else
            {
                data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new ServiceListItemDto
                    {
                        SrvID = s.SrvID,
                        SrvClaFID = s.SrvClaFID,
                        SrvFromDate = s.SrvFromDate,
                        SrvToDate = s.SrvToDate,
                        SrvProcedureCode = s.SrvProcedureCode,
                        SrvModifier1 = s.SrvModifier1,
                        SrvModifier2 = s.SrvModifier2,
                        SrvModifier3 = s.SrvModifier3,
                        SrvModifier4 = s.SrvModifier4,
                        SrvDesc = s.SrvDesc,
                        SrvCharges = s.SrvCharges,
                        SrvAllowedAmt = s.SrvAllowedAmt,
                        SrvUnits = s.SrvUnits,
                        SrvTotalInsAmtPaidTRIG = s.SrvTotalInsAmtPaidTRIG,
                        SrvTotalPatAmtPaidTRIG = s.SrvTotalPatAmtPaidTRIG,
                        SrvTotalBalanceCC = s.SrvTotalBalanceCC,
                        SrvTotalAmtPaidCC = s.SrvTotalAmtPaidCC,
                        SrvResponsibleParty = s.SrvResponsibleParty,
                        SrvNationalDrugCode = s.SrvNationalDrugCode,
                        SrvDrugUnitCount = s.SrvDrugUnitCount,
                        SrvDrugUnitMeasurement = s.SrvDrugUnitMeasurement,
                        SrvPrescriptionNumber = s.SrvPrescriptionNumber,
                        SrvRevenueCode = s.SrvRevenueCode,
                        AdditionalColumns = new Dictionary<string, object?>()
                    })
                    .ToListAsync();
            }

            return Ok(new ApiResponse<List<ServiceListItemDto>>
            {
                Data = data,
                Meta = new PaginationMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            });
        }

        // =========================================================
        // 🟢 AVAILABLE COLUMNS (UI CONFIG ONLY)
        // =========================================================
        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var columns = RelatedColumnConfig.GetAvailableColumns()["Service"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }

        [HttpPost("/api/claims/{claimId:int}/services")]
        public async Task<IActionResult> CreateServiceLine([FromRoute] int claimId, [FromBody] UpsertServiceLineRequest request)
        {
            if (request == null) return BadRequest(new { message = "Request body is required." });
            var userTenantId = _currentContext.TenantId;
            if (userTenantId <= 0)
                throw new UnauthorizedAccessException("Tenant context is required.");

            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            var claimScope = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claimId && c.TenantId == tid && c.FacilityId == fid)
                .Select(c => new { c.TenantId, c.FacilityId })
                .FirstOrDefaultAsync();
            if (claimScope == null)
                return NotFound(new { message = "Claim not found." });
            if (claimScope.TenantId != userTenantId)
                throw new UnauthorizedAccessException("Tenant context is required.");

            var now = DateTime.UtcNow;
            var fromDate = request.SrvFromDate ?? DateOnly.FromDateTime(now);
            var toDate = request.SrvToDate ?? fromDate;
            var units = request.SrvUnits.HasValue && request.SrvUnits.Value > 0 ? request.SrvUnits : 1f;
            var charges = request.SrvCharges ?? 0m;
            var allowed = request.SrvAllowedAmt ?? 0m;
            int? resolvedResponsibleParty = request.SrvResponsibleParty.HasValue && request.SrvResponsibleParty.Value > 0
                ? request.SrvResponsibleParty.Value
                : null;

            if (!resolvedResponsibleParty.HasValue)
            {
                resolvedResponsibleParty = await _db.Service_Lines.AsNoTracking()
                    .Where(s => s.SrvClaFID == claimId && s.SrvResponsibleParty > 0)
                    .OrderByDescending(s => s.SrvDateTimeModified)
                    .Select(s => (int?)s.SrvResponsibleParty)
                    .FirstOrDefaultAsync();
            }
            if (!resolvedResponsibleParty.HasValue)
            {
                resolvedResponsibleParty = await _db.Claim_Insureds.AsNoTracking()
                    .Where(ci => ci.ClaInsClaFID == claimId && ci.ClaInsSequence == 1 && ci.ClaInsPayFID > 0)
                    .Select(ci => (int?)ci.ClaInsPayFID)
                    .FirstOrDefaultAsync();
            }
            if (!resolvedResponsibleParty.HasValue)
            {
                var patId = await _db.Claims.AsNoTracking()
                    .Where(c => c.ClaID == claimId && c.TenantId == tid && c.FacilityId == fid)
                    .Select(c => c.ClaPatFID)
                    .FirstOrDefaultAsync();
                if (patId > 0)
                {
                    resolvedResponsibleParty = await _db.Patient_Insureds.AsNoTracking()
                        .Where(pi => pi.PatInsPatFID == patId && pi.PatInsSequence == 1)
                        .Select(pi => (int?)pi.PatInsIns.InsPayID)
                        .FirstOrDefaultAsync();
                }
            }
            if (!resolvedResponsibleParty.HasValue)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "VALIDATION",
                    Message = "Responsible payer is required. Please set primary insurance before saving service line."
                });
            }

            Service_Line? entity = null;
            var isInsert = false;

            // 1) If request has SrvID, update that row (same-claim only).
            if (request.SrvID.HasValue && request.SrvID.Value > 0)
            {
                var existing = await _db.Service_Lines.FindAsync(request.SrvID.Value);
                if (existing != null && existing.SrvClaFID == claimId && existing.TenantId == tid && existing.FacilityId == fid)
                {
                    entity = existing;
                    ApplyRequestToServiceLine(entity, request);
                    if (entity.SrvResponsibleParty <= 0) entity.SrvResponsibleParty = resolvedResponsibleParty.Value;
                    entity.SrvDateTimeModified = now;
                }
            }

            // 2) No existing row matched by explicit SrvID: insert new.
            if (entity == null)
            {
                entity = new Service_Line
                {
                    TenantId = claimScope.TenantId,
                    FacilityId = claimScope.FacilityId,
                    SrvClaFID = claimId,
                    SrvDateTimeCreated = now,
                    SrvDateTimeModified = now,
                    SrvFromDate = fromDate,
                    SrvToDate = toDate,
                    SrvProcedureCode = request.SrvProcedureCode?.Trim(),
                    SrvModifier1 = request.SrvModifier1?.Trim(),
                    SrvModifier2 = request.SrvModifier2?.Trim(),
                    SrvModifier3 = request.SrvModifier3?.Trim(),
                    SrvModifier4 = request.SrvModifier4?.Trim(),
                    SrvUnits = units,
                    SrvCharges = charges,
                    SrvAllowedAmt = allowed,
                    SrvDesc = request.SrvDesc,
                    SrvResponsibleParty = resolvedResponsibleParty.Value,
                    SrvNationalDrugCode = request.SrvNationalDrugCode,
                    SrvDrugUnitCount = request.SrvDrugUnitCount,
                    SrvDrugUnitMeasurement = request.SrvDrugUnitMeasurement,
                    SrvPrescriptionNumber = request.SrvPrescriptionNumber,
                    SrvRevenueCode = request.SrvRevenueCode,
                    SrvRespChangeDate = now,
                    SrvSortTiebreaker = 0,
                    SrvGUID = Guid.NewGuid(),
                    SrvModifiersCC = string.Empty
                };
                _db.Service_Lines.Add(entity);
                isInsert = true;
            }

            if (entity.TenantId != claimScope.TenantId)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "TENANT_MISMATCH",
                    Message = "Service_Line.TenantId must match Claim.TenantId."
                });
            }

            _logger.LogInformation(
                "Saving service line ClaimId={claimId}, SrvID={SrvID}, Proc={ProcCode}",
                claimId,
                request.SrvID,
                request.SrvProcedureCode);

            await ApplyProcedureLookupIfRequested(entity, request);
            try
            {
                await _db.SaveChangesAsync(); // exactly once per request
                await _serviceLineRepo.RecalculateServiceLineAsync(entity.SrvID);
                await _db.Entry(entity).ReloadAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Service line save failed for ClaimId={ClaimId}, SrvID={SrvID}", claimId, request.SrvID);
                _logger.LogError("SQL error: {Message}", dbEx.InnerException?.Message);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "FK_VALIDATION",
                    Message = "Service line save failed due to invalid related data."
                });
            }

            if (isInsert)
                _logger.LogInformation("Inserted service line SrvID={SrvID} for ClaimId={ClaimId}", entity.SrvID, claimId);
            else
                _logger.LogInformation("Updated service line SrvID={SrvID} for ClaimId={ClaimId}", entity.SrvID, claimId);

            return Ok(ToDto(entity));
        }

        [HttpPut("/api/claims/{claimId:int}/services/{srvId:int}")]
        public async Task<IActionResult> UpdateServiceLine([FromRoute] int claimId, [FromRoute] int srvId, [FromBody] UpsertServiceLineRequest request)
        {
            if (request == null) return BadRequest(new { message = "Request body is required." });
            if (_currentContext.TenantId <= 0)
                throw new UnauthorizedAccessException("Tenant context is required.");
            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            if (!await _db.Claims.AsNoTracking().AnyAsync(c => c.ClaID == claimId && c.TenantId == tid && c.FacilityId == fid))
                return NotFound(new { message = "Service line not found." });
            var entity = await _db.Service_Lines.FindAsync(srvId);
            if (entity != null && (entity.SrvClaFID != claimId || entity.TenantId != tid || entity.FacilityId != fid)) entity = null;
            if (entity == null) return NotFound(new { message = "Service line not found." });

            var oldUnits = entity.SrvUnits.HasValue && entity.SrvUnits.Value > 0 ? (int)Math.Round(entity.SrvUnits.Value) : 1;
            var oldCharge = entity.SrvCharges;
            var oldAllowed = entity.SrvAllowedAmt;

            entity.SrvFromDate = request.SrvFromDate ?? entity.SrvFromDate;
            entity.SrvToDate = request.SrvToDate ?? entity.SrvToDate;
            entity.SrvProcedureCode = request.SrvProcedureCode?.Trim() ?? entity.SrvProcedureCode;
            entity.SrvModifier1 = request.SrvModifier1?.Trim();
            entity.SrvModifier2 = request.SrvModifier2?.Trim();
            entity.SrvModifier3 = request.SrvModifier3?.Trim();
            entity.SrvModifier4 = request.SrvModifier4?.Trim();
            entity.SrvDesc = request.SrvDesc;
            entity.SrvResponsibleParty = request.SrvResponsibleParty ?? entity.SrvResponsibleParty;
            entity.SrvNationalDrugCode = request.SrvNationalDrugCode;
            entity.SrvDrugUnitCount = request.SrvDrugUnitCount;
            entity.SrvDrugUnitMeasurement = request.SrvDrugUnitMeasurement;
            entity.SrvPrescriptionNumber = request.SrvPrescriptionNumber;
            entity.SrvRevenueCode = request.SrvRevenueCode;

            await ApplyProcedureLookupIfRequested(entity, request);

            var hasNewUnits = request.SrvUnits.HasValue && request.SrvUnits.Value > 0;
            if (hasNewUnits)
            {
                var newUnits = (int)Math.Round(request.SrvUnits!.Value);
                entity.SrvUnits = request.SrvUnits;

                // EZClaim units change rule: ONLY charge/allowed recalc from old unit price.
                if (newUnits > 0 && oldUnits > 0 && newUnits != oldUnits)
                {
                    entity.SrvCharges = _claimChargeCalculator.RecalculateCharge(oldCharge, oldUnits, newUnits);
                    entity.SrvAllowedAmt = _claimChargeCalculator.RecalculateCharge(oldAllowed, oldUnits, newUnits);
                }
                else
                {
                    // Do not force 0 over lookup-populated values.
                    if (request.SrvCharges.HasValue && request.SrvCharges.Value > 0) entity.SrvCharges = request.SrvCharges.Value;
                    if (request.SrvAllowedAmt.HasValue) entity.SrvAllowedAmt = request.SrvAllowedAmt.Value;
                }
            }
            else
            {
                // Do not force 0 over lookup-populated values.
                if (request.SrvCharges.HasValue && request.SrvCharges.Value > 0) entity.SrvCharges = request.SrvCharges.Value;
                if (request.SrvAllowedAmt.HasValue) entity.SrvAllowedAmt = request.SrvAllowedAmt.Value;
            }

            entity.SrvDateTimeModified = DateTime.UtcNow;
            _logger.LogInformation("Service line update saving SrvID={SrvID} for ClaimId={ClaimId}, Procedure={ProcedureCode}", entity.SrvID, claimId, entity.SrvProcedureCode);
            try
            {
                await _db.SaveChangesAsync();
                await _serviceLineRepo.RecalculateServiceLineAsync(entity.SrvID);
                await _db.Entry(entity).ReloadAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Service line update failed for ClaimId={ClaimId}, SrvID={SrvID}", claimId, srvId);
                _logger.LogError("SQL error: {Message}", dbEx.InnerException?.Message);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "FK_VALIDATION",
                    Message = "Service line update failed due to invalid related data."
                });
            }
            return Ok(ToDto(entity));
        }

        private static void ApplyRequestToServiceLine(Service_Line entity, UpsertServiceLineRequest request)
        {
            entity.SrvFromDate = request.SrvFromDate ?? entity.SrvFromDate;
            entity.SrvToDate = request.SrvToDate ?? entity.SrvToDate;
            entity.SrvProcedureCode = request.SrvProcedureCode?.Trim() ?? entity.SrvProcedureCode;
            entity.SrvModifier1 = request.SrvModifier1?.Trim();
            entity.SrvModifier2 = request.SrvModifier2?.Trim();
            entity.SrvModifier3 = request.SrvModifier3?.Trim();
            entity.SrvModifier4 = request.SrvModifier4?.Trim();
            entity.SrvDesc = request.SrvDesc;
            entity.SrvResponsibleParty = request.SrvResponsibleParty ?? entity.SrvResponsibleParty;
            entity.SrvNationalDrugCode = request.SrvNationalDrugCode;
            entity.SrvDrugUnitCount = request.SrvDrugUnitCount;
            entity.SrvDrugUnitMeasurement = request.SrvDrugUnitMeasurement;
            entity.SrvPrescriptionNumber = request.SrvPrescriptionNumber;
            entity.SrvRevenueCode = request.SrvRevenueCode;
            if (request.SrvUnits.HasValue) entity.SrvUnits = request.SrvUnits;
            if (request.SrvCharges.HasValue) entity.SrvCharges = request.SrvCharges.Value;
            if (request.SrvAllowedAmt.HasValue) entity.SrvAllowedAmt = request.SrvAllowedAmt.Value;
        }

        [HttpDelete("/api/claims/{claimId:int}/services/{srvId:int}")]
        public async Task<IActionResult> DeleteServiceLine([FromRoute] int claimId, [FromRoute] int srvId)
        {
            if (_currentContext.TenantId <= 0)
                throw new UnauthorizedAccessException("Tenant context is required.");
            var tid = _currentContext.TenantId;
            var fid = _currentContext.FacilityId;
            if (!await _db.Claims.AsNoTracking().AnyAsync(c => c.ClaID == claimId && c.TenantId == tid && c.FacilityId == fid))
                return NotFound(new { message = "Service line not found." });
            var entity = await _db.Service_Lines.FirstOrDefaultAsync(s => s.SrvID == srvId && s.SrvClaFID == claimId);
            if (entity != null && (entity.TenantId != tid || entity.FacilityId != fid)) entity = null;
            if (entity == null) return NotFound(new { message = "Service line not found." });

            var adjs = await _db.Adjustments
                .Where(a =>
                    (a.AdjSrvFID == srvId || a.AdjTaskFID == srvId) &&
                    a.TenantId == tid &&
                    a.FacilityId == fid)
                .ToListAsync();
            if (adjs.Count > 0) _db.Adjustments.RemoveRange(adjs);

            var disbs = await _db.Disbursements.Where(d => d.DisbSrvFID == srvId).ToListAsync();
            if (disbs.Count > 0) _db.Disbursements.RemoveRange(disbs);

            _db.Service_Lines.Remove(entity);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private async Task ApplyProcedureLookupIfRequested(Service_Line entity, UpsertServiceLineRequest request)
        {
            if (string.IsNullOrWhiteSpace(entity.SrvProcedureCode)) return;

            // Backfill fee/description when frontend sends a pasted procedure code with zero charge.
            var missingCharge = entity.SrvCharges <= 0m;
            if (!request.ApplyProcedureLookup && !missingCharge) return;

            var lookup = await _procedureLookupService.LookupAsync(
                entity.SrvProcedureCode,
                null,
                null,
                null,
                DateTime.UtcNow.Date,
                entity.SrvProductCode);

            if (lookup == null) return;

            var units = entity.SrvUnits.HasValue && entity.SrvUnits.Value > 0 ? (int)Math.Round(entity.SrvUnits.Value) : 1;
            var calc = _claimChargeCalculator.Calculate(lookup, units, entity.SrvCharges, entity.SrvAllowedAmt, false);

            entity.SrvProcedureCode = lookup.ProcCode;
            entity.SrvUnits = lookup.ProcUnits > 0 ? lookup.ProcUnits : entity.SrvUnits;
            entity.SrvCharges = calc.Charge;
            entity.SrvAllowedAmt = calc.Allowed;
            entity.SrvDesc = lookup.ProcDescription;
            if (lookup is Procedure_Code procEntity)
            {
                entity.SrvModifier1 = procEntity.ProcModifier1;
                entity.SrvModifier2 = procEntity.ProcModifier2;
                entity.SrvModifier3 = procEntity.ProcModifier3;
                entity.SrvModifier4 = procEntity.ProcModifier4;
                entity.SrvNationalDrugCode = procEntity.ProcNDCCode;
                entity.SrvDrugUnitMeasurement = procEntity.ProcDrugUnitMeasurement;
                entity.SrvDrugUnitCount = procEntity.ProcDrugUnitCount;
                entity.SrvRevenueCode = procEntity.ProcRevenueCode;
            }
        }

        private static ServiceListItemDto ToDto(Service_Line s)
        {
            return new ServiceListItemDto
            {
                SrvID = s.SrvID,
                SrvClaFID = s.SrvClaFID,
                SrvDateTimeCreated = s.SrvDateTimeCreated,
                SrvFromDate = s.SrvFromDate,
                SrvToDate = s.SrvToDate,
                SrvProcedureCode = s.SrvProcedureCode,
                SrvModifier1 = s.SrvModifier1,
                SrvModifier2 = s.SrvModifier2,
                SrvModifier3 = s.SrvModifier3,
                SrvModifier4 = s.SrvModifier4,
                SrvUnits = s.SrvUnits,
                SrvCharges = s.SrvCharges,
                SrvAllowedAmt = s.SrvAllowedAmt,
                SrvTotalInsAmtPaidTRIG = s.SrvTotalInsAmtPaidTRIG,
                SrvTotalPatAmtPaidTRIG = s.SrvTotalPatAmtPaidTRIG,
                SrvTotalBalanceCC = s.SrvTotalBalanceCC,
                SrvTotalAmtPaidCC = s.SrvTotalAmtPaidCC,
                SrvResponsibleParty = s.SrvResponsibleParty,
                SrvDesc = s.SrvDesc,
                SrvNationalDrugCode = s.SrvNationalDrugCode,
                SrvDrugUnitCount = s.SrvDrugUnitCount,
                SrvDrugUnitMeasurement = s.SrvDrugUnitMeasurement,
                SrvPrescriptionNumber = s.SrvPrescriptionNumber,
                SrvRevenueCode = s.SrvRevenueCode,
                AdditionalColumns = new Dictionary<string, object?>()
            };
        }

        public sealed class UpsertServiceLineRequest
        {
            public int? SrvID { get; set; }
            public DateOnly? SrvFromDate { get; set; }
            public DateOnly? SrvToDate { get; set; }
            public string? SrvProcedureCode { get; set; }
            public string? SrvModifier1 { get; set; }
            public string? SrvModifier2 { get; set; }
            public string? SrvModifier3 { get; set; }
            public string? SrvModifier4 { get; set; }
            public float? SrvUnits { get; set; }
            public decimal? SrvCharges { get; set; }
            public decimal? SrvAllowedAmt { get; set; }
            public int? SrvResponsibleParty { get; set; }
            public string? SrvDesc { get; set; }
            public string? SrvNationalDrugCode { get; set; }
            public double? SrvDrugUnitCount { get; set; }
            public string? SrvDrugUnitMeasurement { get; set; }
            public string? SrvPrescriptionNumber { get; set; }
            public string? SrvRevenueCode { get; set; }
            public bool ApplyProcedureLookup { get; set; }
        }
    }
}
