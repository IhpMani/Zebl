using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Services;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/claims")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class ClaimsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly IClaimExportService _claimExportService;
        private readonly ISecondaryTriggerService _secondaryTriggerService;
        private readonly ILogger<ClaimsController> _logger;
        private readonly Zebl.Infrastructure.Services.ProgramSettingsService _programSettingsService;
        private readonly Zebl.Application.Repositories.IClaimRejectionRepository _claimRejectionRepository;
        private readonly IClaimScrubService _claimScrubService;

        public ClaimsController(
            ZeblDbContext db,
            ICurrentUserContext userContext,
            IClaimExportService claimExportService,
            ISecondaryTriggerService secondaryTriggerService,
            ILogger<ClaimsController> logger,
            Zebl.Infrastructure.Services.ProgramSettingsService programSettingsService,
            Zebl.Application.Repositories.IClaimRejectionRepository claimRejectionRepository,
            IClaimScrubService claimScrubService)
        {
            _db = db;
            _userContext = userContext;
            _claimExportService = claimExportService;
            _secondaryTriggerService = secondaryTriggerService;
            _logger = logger;
            _programSettingsService = programSettingsService;
            _claimRejectionRepository = claimRejectionRepository;
            _claimScrubService = claimScrubService;
        }

        [HttpGet("rejections")]
        public async Task<IActionResult> GetRejections()
        {
            var items = await _claimRejectionRepository.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("rejections/{id:int}")]
        public async Task<IActionResult> GetRejectionById(int id)
        {
            var item = await _claimRejectionRepository.GetByIdAsync(id);
            if (item == null)
                return NotFound();
            return Ok(item);
        }

        [HttpPost("rejections/{id:int}/resolve")]
        public async Task<IActionResult> ResolveRejection(int id)
        {
            var existing = await _claimRejectionRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound();

            existing.Status = "Resolved";
            existing.ResolvedAt = DateTime.UtcNow;
            await _claimRejectionRepository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpPost("scrub")]
        public async Task<IActionResult> ScrubClaim([FromBody] ScrubRequest request)
        {
            if (request == null || request.ClaimId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "ClaimId is required."
                });
            }

            var results = await _claimScrubService.ScrubClaimAsync(request.ClaimId);
            return Ok(results);
        }

        public sealed class ScrubRequest
        {
            public int ClaimId { get; set; }
        }

        /// <summary>
        /// Evaluate claim for secondary: rule-driven PR/CO forwardable amount, create secondary if eligible.
        /// Call after ERA is posted and reconciliation passes, or after manual posting.
        /// </summary>
        [HttpPost("{id:int}/evaluate-secondary")]
        public async Task<IActionResult> EvaluateSecondary(int id)
        {
            var result = await _secondaryTriggerService.EvaluateAndTriggerAsync(id);
            return Ok(new
            {
                triggered = result.Triggered,
                reason = result.Reason,
                forwardAmount = result.ForwardAmount,
                secondaryClaimId = result.SecondaryClaimId
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetClaims(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? status = null,
            [FromQuery] string? statusList = null, // Comma-separated list of statuses (Excel-style)
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? searchText = null, // Text search across multiple columns
            [FromQuery] int? minClaimId = null,
            [FromQuery] int? maxClaimId = null,
            [FromQuery] decimal? minTotalCharge = null,
            [FromQuery] decimal? maxTotalCharge = null,
            [FromQuery] decimal? minTotalBalance = null,
            [FromQuery] decimal? maxTotalBalance = null,
            [FromQuery] int? patientId = null, // Filter by patient (ClaPatFID)
            [FromQuery] string? patAccountNo = null, // Filter by patient account number (exact match; from Account # column filter)
            [FromQuery] string? additionalColumns = null) // Comma-separated list of additional column keys to include
        {
            // Validate input
            if (page < 1)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page must be at least 1"
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page size must be between 1 and 100"
                });
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "From date must be before or equal to to date"
                });
            }

            // Parse additional columns
            var requestedColumns = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                requestedColumns = additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();
            }

            // Use requested keys directly so Claim List Add Column is not constrained by a stale server whitelist.
            var columnsToInclude = requestedColumns
                .Select(k => new RelatedColumnDefinition { Key = k, Label = k, Table = "Claim", Path = k })
                .ToList();
            
            // Pre-evaluate which columns are requested to avoid evaluating in Select()
            var hasPatFirstName = columnsToInclude.Any(col => col.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(col => col.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(col => col.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(col => col.Key == "patAccountNo");
            var hasPatPhoneNo = columnsToInclude.Any(col => col.Key == "patPhoneNo");
            var hasPatCity = columnsToInclude.Any(col => col.Key == "patCity");
            var hasPatState = columnsToInclude.Any(col => col.Key == "patState");
            var hasPatBirthDate = columnsToInclude.Any(col => col.Key == "patBirthDate");
            var hasPatDob = columnsToInclude.Any(col => col.Key == "patDOB");
            var hasPatClassification = columnsToInclude.Any(col => col.Key == "patClassification");
            var hasPrimaryPayerName = columnsToInclude.Any(col => col.Key == "primaryPayerName");
            var hasAttendingPhysicianName = columnsToInclude.Any(col => col.Key == "attendingPhysicianName");
            var hasReferringPhysicianName = columnsToInclude.Any(col => col.Key == "referringPhysicianName");
            var hasRenderingPhysicianName = columnsToInclude.Any(col => col.Key == "renderingPhysicianName");
            var hasOperatingPhysicianName = columnsToInclude.Any(col => col.Key == "operatingPhysicianName");
            var hasOrderingPhysicianName = columnsToInclude.Any(col => col.Key == "orderingPhysicianName");
            var hasBillingPhysicianName = columnsToInclude.Any(col => col.Key == "billingPhysicianName");
            var hasSupervisingPhysicianName = columnsToInclude.Any(col => col.Key == "supervisingPhysicianName");
            var hasSecondaryPayerName = columnsToInclude.Any(col => col.Key == "secondaryPayerName");
            var hasPrimaryPayerID = columnsToInclude.Any(col => col.Key == "primaryPayerID");
            var hasPrimaryPayerPhone = columnsToInclude.Any(col => col.Key == "primaryPayerPhone");
            var hasPriInsClaimFilingInd = columnsToInclude.Any(col => col.Key == "priInsClaimFilingInd");
            var hasSecInsClaimFilingInd = columnsToInclude.Any(col => col.Key == "secInsClaimFilingInd");
            var hasPrimaryInsuredID = columnsToInclude.Any(col => col.Key == "primaryInsuredID");
            var hasPrimaryInsuredName = columnsToInclude.Any(col => col.Key == "primaryInsuredName");
            var hasPrimaryInsuredDOB = columnsToInclude.Any(col => col.Key == "primaryInsuredDOB");
            var hasPrimaryInsuredEmployer = columnsToInclude.Any(col => col.Key == "primaryInsuredEmployer");
            var hasPrimaryInsuredPlan = columnsToInclude.Any(col => col.Key == "primaryInsuredPlan");
            var hasRenderingPhyName = columnsToInclude.Any(col => col.Key == "renderingPhyName");
            var hasRenderingPhyNPI = columnsToInclude.Any(col => col.Key == "renderingPhyNPI");
            var hasBillingPhyName = columnsToInclude.Any(col => col.Key == "billingPhyName");
            var hasBillingPhyNPI = columnsToInclude.Any(col => col.Key == "billingPhyNPI");
            var hasFacilityName = columnsToInclude.Any(col => col.Key == "facilityName");

            // Build efficient LINQ query with server-side filtering
            // Note: We don't need Include() when using Select() - EF Core will automatically join
            var query = _db.Claims.AsNoTracking();

            // Excel-style status filter: support both single status and comma-separated list
            if (!string.IsNullOrWhiteSpace(statusList))
            {
                var statuses = statusList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (statuses.Any())
                {
                    query = query.Where(c => c.ClaStatus != null && statuses.Contains(c.ClaStatus));
                }
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.ClaStatus == status);
            }

            // Date range filter
            if (fromDate.HasValue)
            {
                query = query.Where(c => c.ClaDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.ClaDateTimeCreated <= toDate.Value);
            }

            // Claim ID range filter
            if (minClaimId.HasValue)
            {
                query = query.Where(c => c.ClaID >= minClaimId.Value);
            }

            if (maxClaimId.HasValue)
            {
                query = query.Where(c => c.ClaID <= maxClaimId.Value);
            }

            // Total Charge range filter
            if (minTotalCharge.HasValue)
            {
                query = query.Where(c =>
                    (_db.Service_Lines
                        .Where(s => s.SrvClaFID == c.ClaID)
                        .Sum(s => (decimal?)s.SrvCharges) ?? 0m) >= minTotalCharge.Value);
            }

            if (maxTotalCharge.HasValue)
            {
                query = query.Where(c =>
                    (_db.Service_Lines
                        .Where(s => s.SrvClaFID == c.ClaID)
                        .Sum(s => (decimal?)s.SrvCharges) ?? 0m) <= maxTotalCharge.Value);
            }

            // Total Balance range filter
            if (minTotalBalance.HasValue)
            {
                query = query.Where(c =>
                    (_db.Service_Lines
                        .Where(s => s.SrvClaFID == c.ClaID)
                        .Sum(s => s.SrvTotalBalanceCC) ?? 0m) >= minTotalBalance.Value);
            }

            if (maxTotalBalance.HasValue)
            {
                query = query.Where(c =>
                    (_db.Service_Lines
                        .Where(s => s.SrvClaFID == c.ClaID)
                        .Sum(s => s.SrvTotalBalanceCC) ?? 0m) <= maxTotalBalance.Value);
            }

            // Patient filter (for ribbon: open claims for a specific patient)
            if (patientId.HasValue)
            {
                query = query.Where(c => c.ClaPatFID == patientId.Value);
            }

            // Patient account number filter (from Claim List Account # column filter – exact match)
            if (!string.IsNullOrWhiteSpace(patAccountNo))
            {
                var accountNoTrimmed = patAccountNo.Trim();
                query = query.Where(c => c.ClaPatF != null && c.ClaPatF.PatAccountNo != null && c.ClaPatF.PatAccountNo == accountNoTrimmed);
            }

            // Text search across multiple columns (optimized for SQL)
            // Note: Avoid ToString().Contains() as it's very slow - use direct comparisons instead
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower().Trim();
                
                // Try to parse as number for ID and numeric fields (much faster)
                if (int.TryParse(searchText, out int searchInt))
                {
                    query = query.Where(c => c.ClaID == searchInt);
                }
                else if (decimal.TryParse(searchText, out decimal searchDecimal))
                {
                    query = query.Where(c =>
                        ((_db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => (decimal?)s.SrvCharges) ?? 0m) == searchDecimal) ||
                        (c.ClaTotalAmtPaidCC.HasValue && c.ClaTotalAmtPaidCC.Value == searchDecimal) ||
                        ((_db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => s.SrvTotalBalanceCC) ?? 0m) == searchDecimal));
                }
                else
                {
                    // Text search only on string fields (can use indexes if available)
                    query = query.Where(c => c.ClaStatus != null && c.ClaStatus.ToLower().Contains(searchLower));
                }
            }

            // Order by ID descending for consistent pagination
            query = query.OrderByDescending(c => c.ClaID);

            // Get count with timeout protection and fallback
            // For large tables, use approximate count when no filters are applied
            int totalCount;
            bool hasFilters = !string.IsNullOrWhiteSpace(status) || 
                             !string.IsNullOrWhiteSpace(statusList) ||
                             fromDate.HasValue || 
                             toDate.HasValue ||
                             minClaimId.HasValue || 
                             maxClaimId.HasValue ||
                             minTotalCharge.HasValue || 
                             maxTotalCharge.HasValue ||
                             minTotalBalance.HasValue || 
                             maxTotalBalance.HasValue ||
                             patientId.HasValue ||
                             !string.IsNullOrWhiteSpace(patAccountNo) ||
                             !string.IsNullOrWhiteSpace(searchText);

            if (!hasFilters)
            {
                // No filters - use fast approximate count from metadata
                try
                {
                    totalCount = await GetApproxClaimCountAsync();
                }
                catch
                {
                    // If metadata query fails, use a large estimate
                    totalCount = 1000000; // Large estimate so pagination still works
                }
            }
            else
            {
                // Has filters - try exact count with timeout protection
                try
                {
                    // Use a cancellation token with timeout (15 seconds for filtered queries)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    totalCount = await query.CountAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // If count times out, use approximate count
                    _logger.LogWarning("Count query timed out, using approximate count");
                    try
                    {
                        totalCount = await GetApproxClaimCountAsync();
                    }
                    catch
                    {
                        // If that also fails, estimate based on page
                        totalCount = pageSize * (page + 10); // Estimate: current page + 10 more
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting claim count");
                    // Fallback to approximate count
                    try
                    {
                        totalCount = await GetApproxClaimCountAsync();
                    }
                    catch
                    {
                        totalCount = pageSize * (page + 10); // Rough estimate
                    }
                }
            }

            // Apply pagination and project to DTO
            List<ClaimListItemDto> result;
            
            if (columnsToInclude.Any())
            {
                // Use explicit LEFT JOINs instead of Include to avoid excluding claims when
                // ClaRenderingPhyFID/ClaBillingPhyFID/ClaFacilityPhyFID=0 or FK references missing row
                var joinQuery = from c in query
                               join p in _db.Patients.AsNoTracking() on c.ClaPatFID equals p.PatID into pGrp
                               from p in pGrp.DefaultIfEmpty()
                               join rend in _db.Physicians.AsNoTracking() on c.ClaRenderingPhyFID equals rend.PhyID into rendGrp
                               from rend in rendGrp.DefaultIfEmpty()
                               join bill in _db.Physicians.AsNoTracking() on c.ClaBillingPhyFID equals bill.PhyID into billGrp
                               from bill in billGrp.DefaultIfEmpty()
                               join fac in _db.Physicians.AsNoTracking() on c.ClaFacilityPhyFID equals fac.PhyID into facGrp
                               from fac in facGrp.DefaultIfEmpty()
                               select new
                               {
                                   Claim = new ClaimListItemDto
                                   {
                                       ClaID = c.ClaID,
                                       ClaStatus = c.ClaStatus,
                                       ClaDateTimeCreated = c.ClaDateTimeCreated,
                                       ClaTotalChargeTRIG = _db.Service_Lines
                                           .Where(s => s.SrvClaFID == c.ClaID)
                                           .Sum(s => (decimal?)s.SrvCharges) ?? 0m,
                                       ClaTotalInsBalanceTRIG = _db.Service_Lines
                                           .Where(s => s.SrvClaFID == c.ClaID)
                                           .Sum(s => s.SrvTotalInsBalanceCC) ?? 0m,
                                       ClaTotalPatBalanceTRIG = _db.Service_Lines
                                           .Where(s => s.SrvClaFID == c.ClaID)
                                           .Sum(s => s.SrvTotalPatBalanceCC) ?? 0m,
                                       ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                                       ClaTotalBalanceCC = _db.Service_Lines
                                           .Where(s => s.SrvClaFID == c.ClaID)
                                           .Sum(s => s.SrvTotalBalanceCC) ?? 0m,
                                       ClaClassification = c.ClaClassification ?? (fac != null ? fac.PhyName : null),
                                       ClaDateTotalFrom = c.ClaDateTotalFrom,
                                       ClaBillTo = c.ClaBillTo,
                                       PatFullNameCC = p != null ? p.PatFullNameCC : null,
                                       PrimaryPayerName = _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsPay.PayName)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsPayF.PayName)
                                               .FirstOrDefault(),
                                       ClaPatFID = c.ClaPatFID,
                                       ClaAttendingPhyFID = c.ClaAttendingPhyFID,
                                       ClaBillingPhyFID = c.ClaBillingPhyFID,
                                       ClaReferringPhyFID = c.ClaReferringPhyFID,
                                       ClaBillDate = c.ClaBillDate,
                                       ClaTypeOfBill = c.ClaTypeOfBill,
                                       ClaAdmissionType = c.ClaAdmissionType,
                                       ClaPatientStatus = c.ClaPatientStatus,
                                       ClaCreatedUserName = c.ClaCreatedUserName,
                                       ClaLastUserName = c.ClaLastUserName,
                                       ClaDiagnosis1 = c.ClaDiagnosis1,
                                       ClaDiagnosis2 = c.ClaDiagnosis2,
                                       ClaDiagnosis3 = c.ClaDiagnosis3,
                                       ClaDiagnosis4 = c.ClaDiagnosis4,
                                       ClaFirstDateTRIG = c.ClaFirstDateTRIG,
                                       ClaLastDateTRIG = c.ClaLastDateTRIG
                                   },
                                   ClaimEntity = c,
                                   PatFirstName = hasPatFirstName ? (p != null ? p.PatFirstName : null) : null,
                                   PatLastName = hasPatLastName ? (p != null ? p.PatLastName : null) : null,
                                   PatFullNameCC = hasPatFullNameCC ? (p != null ? p.PatFullNameCC : null) : null,
                                   PrimaryPayerName = hasPrimaryPayerName
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsPay.PayName)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsPayF.PayName)
                                               .FirstOrDefault()
                                       : null,
                                   PatAccountNo = hasPatAccountNo ? (p != null ? p.PatAccountNo : null) : null,
                                   PatPhoneNo = hasPatPhoneNo ? (p != null ? p.PatPhoneNo : null) : null,
                                   PatCity = hasPatCity ? (p != null ? p.PatCity : null) : null,
                                   PatState = hasPatState ? (p != null ? p.PatState : null) : null,
                                   PatBirthDate = hasPatBirthDate ? (p != null ? p.PatBirthDate : (DateOnly?)null) : null,
                                   PatDob = hasPatDob ? (p != null ? p.PatBirthDate : (DateOnly?)null) : null,
                                   PatClassification = hasPatClassification ? (p != null ? p.PatClassification : null) : null,
                                   AttendingPhysicianName = hasAttendingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaAttendingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   ReferringPhysicianName = hasReferringPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaReferringPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   RenderingPhysicianName = hasRenderingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaRenderingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   OperatingPhysicianName = hasOperatingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaOperatingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   OrderingPhysicianName = hasOrderingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaOrderingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   BillingPhysicianName = hasBillingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaBillingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   SupervisingPhysicianName = hasSupervisingPhysicianName
                                       ? _db.Physicians.Where(px => px.PhyID == c.ClaSupervisingPhyFID).Select(px => px.PhyName).FirstOrDefault()
                                       : null,
                                   SecondaryPayerName = hasSecondaryPayerName
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 2)
                                           .Select(pi => pi.PatInsIns.InsPay.PayName)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 2)
                                               .Select(ci => ci.ClaInsPayF.PayName)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryPayerID = hasPrimaryPayerID
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsPay.PayExternalID)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsPayF.PayExternalID)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryPayerPhone = hasPrimaryPayerPhone
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsPay.PayPhoneNo)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsPayF.PayPhoneNo)
                                               .FirstOrDefault()
                                       : null,
                                   PriInsClaimFilingInd = hasPriInsClaimFilingInd
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsClaimFilingIndicator)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsClaimFilingIndicator)
                                               .FirstOrDefault()
                                       : null,
                                   SecInsClaimFilingInd = hasSecInsClaimFilingInd
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 2)
                                           .Select(pi => pi.PatInsIns.InsClaimFilingIndicator)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 2)
                                               .Select(ci => ci.ClaInsClaimFilingIndicator)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryInsuredID = hasPrimaryInsuredID
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsIDNumber)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsIDNumber)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryInsuredName = hasPrimaryInsuredName
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => ((pi.PatInsIns.InsLastName ?? "") + ", " + (pi.PatInsIns.InsFirstName ?? "")).Trim().Trim(','))
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ((ci.ClaInsLastName ?? "") + ", " + (ci.ClaInsFirstName ?? "")).Trim().Trim(','))
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryInsuredDOB = hasPrimaryInsuredDOB
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsBirthDate)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsBirthDate)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryInsuredEmployer = hasPrimaryInsuredEmployer
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsEmployer)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsEmployer)
                                               .FirstOrDefault()
                                       : null,
                                   PrimaryInsuredPlan = hasPrimaryInsuredPlan
                                       ? _db.Patient_Insureds
                                           .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                                           .Select(pi => pi.PatInsIns.InsPlanName)
                                           .FirstOrDefault()
                                           ?? _db.Claim_Insureds
                                               .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                               .Select(ci => ci.ClaInsPlanName)
                                               .FirstOrDefault()
                                       : null,
                                   RenderingPhyName = hasRenderingPhyName ? (rend != null ? rend.PhyName : null) : null,
                                   RenderingPhyNPI = hasRenderingPhyNPI ? (rend != null ? rend.PhyNPI : null) : null,
                                   BillingPhyName = hasBillingPhyName ? (bill != null ? bill.PhyName : null) : null,
                                   BillingPhyNPI = hasBillingPhyNPI ? (bill != null ? bill.PhyNPI : null) : null,
                                   FacilityPhyName = hasFacilityName ? (fac != null ? fac.PhyName : null) : null
                               };

                var data = await joinQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to final DTOs with additional columns
                result = data.Select(d =>
                {
                    var claim = d.Claim;
                    claim.AdditionalColumns = new Dictionary<string, object?>();
                    foreach (var col in columnsToInclude)
                    {
                        object? value = col.Key switch
                        {
                            "patFirstName" => d.PatFirstName,
                            "patLastName" => d.PatLastName,
                            "patFullNameCC" => d.PatFullNameCC,
                            "primaryPayerName" => d.PrimaryPayerName,
                            "patAccountNo" => d.PatAccountNo,
                            "patPhoneNo" => d.PatPhoneNo,
                            "patCity" => d.PatCity,
                            "patState" => d.PatState,
                            "patBirthDate" => d.PatBirthDate,
                            "patDOB" => d.PatDob,
                            "patClassification" => d.PatClassification,
                            "patID" => d.Claim.ClaPatFID,
                            "claDateTotalFrom" => d.Claim.ClaDateTotalFrom,
                            "claLastDateTRIG" => d.Claim.ClaLastDateTRIG,
                            "claFirstDOS" => d.Claim.ClaFirstDateTRIG,
                            "claLastDOS" => d.Claim.ClaLastDateTRIG,
                            "claTotalChargeTRIG" => d.Claim.ClaTotalChargeTRIG,
                            "claTotalInsBalanceTRIG" => d.Claim.ClaTotalInsBalanceTRIG,
                            "claTotalPatBalanceTRIG" => d.Claim.ClaTotalPatBalanceTRIG,
                            "claTotalBalanceCC" => d.Claim.ClaTotalBalanceCC,
                            "claTotalCharge" => d.Claim.ClaTotalChargeTRIG,
                            "claTotalInsBalance" => d.Claim.ClaTotalInsBalanceTRIG,
                            "claTotalPatBalance" => d.Claim.ClaTotalPatBalanceTRIG,
                            "claTotalBalance" => d.Claim.ClaTotalBalanceCC,
                            "attendingPhysicianName" => d.AttendingPhysicianName,
                            "referringPhysicianName" => d.ReferringPhysicianName,
                            "renderingPhysicianName" => d.RenderingPhysicianName,
                            "operatingPhysicianName" => d.OperatingPhysicianName,
                            "orderingPhysicianName" => d.OrderingPhysicianName,
                            "billingPhysicianName" => d.BillingPhysicianName,
                            "supervisingPhysicianName" => d.SupervisingPhysicianName,
                            "secondaryPayerName" => d.SecondaryPayerName,
                            "primaryPayerID" => d.PrimaryPayerID,
                            "primaryPayerPhone" => d.PrimaryPayerPhone,
                            "priInsClaimFilingInd" => d.PriInsClaimFilingInd,
                            "secInsClaimFilingInd" => d.SecInsClaimFilingInd,
                            "primaryInsuredID" => d.PrimaryInsuredID,
                            "primaryInsuredName" => string.IsNullOrWhiteSpace(d.PrimaryInsuredName) ? null : d.PrimaryInsuredName,
                            "primaryInsuredDOB" => d.PrimaryInsuredDOB,
                            "primaryInsuredEmployer" => d.PrimaryInsuredEmployer,
                            "primaryInsuredPlan" => d.PrimaryInsuredPlan,
                            "renderingPhyName" => d.RenderingPhyName,
                            "renderingPhyNPI" => d.RenderingPhyNPI,
                            "billingPhyName" => d.BillingPhyName,
                            "billingPhyNPI" => d.BillingPhyNPI,
                            "facilityName" => d.FacilityPhyName,
                            "claClassification" => d.Claim.ClaClassification ?? d.FacilityPhyName,
                            _ => null
                        };
                        if (value == null)
                        {
                            value = GetClaimColumnValue(d.ClaimEntity, col.Key);
                        }
                        claim.AdditionalColumns[col.Key] = value;
                    }
                    return claim;
                }).ToList();
            }
            else
            {
                // No additional columns - simple projection without related entities
                result = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new ClaimListItemDto
                    {
                        ClaID = c.ClaID,
                        ClaStatus = c.ClaStatus,
                        ClaDateTimeCreated = c.ClaDateTimeCreated,
                        ClaTotalChargeTRIG = _db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => (decimal?)s.SrvCharges) ?? 0m,
                        ClaTotalInsBalanceTRIG = _db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => s.SrvTotalInsBalanceCC) ?? 0m,
                        ClaTotalPatBalanceTRIG = _db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => s.SrvTotalPatBalanceCC) ?? 0m,
                        ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                        ClaTotalBalanceCC = _db.Service_Lines
                            .Where(s => s.SrvClaFID == c.ClaID)
                            .Sum(s => s.SrvTotalBalanceCC) ?? 0m,
                        ClaClassification = c.ClaClassification ?? _db.Physicians
                            .Where(px => px.PhyID == c.ClaFacilityPhyFID)
                            .Select(px => px.PhyName)
                            .FirstOrDefault(),
                        ClaDateTotalFrom = c.ClaDateTotalFrom,
                        ClaBillTo = c.ClaBillTo,
                        PatFullNameCC = c.ClaPatF.PatFullNameCC,
                        PrimaryPayerName = _db.Patient_Insureds
                            .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                            .Select(pi => pi.PatInsIns.InsPay.PayName)
                            .FirstOrDefault()
                            ?? _db.Claim_Insureds
                                .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                                .Select(ci => ci.ClaInsPayF.PayName)
                                .FirstOrDefault(),
                        ClaPatFID = c.ClaPatFID,
                        ClaAttendingPhyFID = c.ClaAttendingPhyFID,
                        ClaBillingPhyFID = c.ClaBillingPhyFID,
                        ClaReferringPhyFID = c.ClaReferringPhyFID,
                        ClaBillDate = c.ClaBillDate,
                        ClaTypeOfBill = c.ClaTypeOfBill,
                        ClaAdmissionType = c.ClaAdmissionType,
                        ClaPatientStatus = c.ClaPatientStatus,
                        ClaCreatedUserName = c.ClaCreatedUserName,
                        ClaLastUserName = c.ClaLastUserName,
                        ClaDiagnosis1 = c.ClaDiagnosis1,
                        ClaDiagnosis2 = c.ClaDiagnosis2,
                        ClaDiagnosis3 = c.ClaDiagnosis3,
                        ClaDiagnosis4 = c.ClaDiagnosis4,
                        ClaFirstDateTRIG = c.ClaFirstDateTRIG,
                        ClaLastDateTRIG = c.ClaLastDateTRIG
                    })
                    .ToListAsync();
            }

            return Ok(new ApiResponse<List<ClaimListItemDto>>
            {
                Data = result,
                Meta = new PaginationMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            });
        }

        [HttpGet("user-kpis")]
        public async Task<IActionResult> GetUserKpis([FromQuery] int trendDays = 30)
        {
            var userName = _userContext.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Ok(new UserKpiDashboardDto());
            }

            if (trendDays < 7) trendDays = 7;
            if (trendDays > 90) trendDays = 90;

            var nowUtc = DateTime.UtcNow;
            var trendStartUtc = nowUtc.Date.AddDays(-(trendDays - 1));
            // Source of truth for "claims edited" = Claim_Audit (same table used by Find Claim Note).
            var normalizedUserName = userName.ToLower();
            var userEditAudits = _db.Claim_Audits
                .AsNoTracking()
                .Where(a => a.ClaFID > 0)
                .Where(a => a.UserName != null && a.UserName.ToLower() == normalizedUserName)
                .Where(a =>
                    a.ActivityType != null &&
                    (EF.Functions.Like(a.ActivityType, "%Claim Edited%") || EF.Functions.Like(a.ActivityType, "%Edit%")));

            var editedClaimIds = userEditAudits
                .Select(a => a.ClaFID)
                .Distinct();
            var userClaims = _db.Claims
                .AsNoTracking()
                .Where(c => editedClaimIds.Contains(c.ClaID));

            var totalClaims = await userClaims.CountAsync();
            if (totalClaims == 0)
            {
                return Ok(new UserKpiDashboardDto
                {
                    UserName = userName
                });
            }

            var totalPaid = await userClaims.SumAsync(c => c.ClaTotalAmtPaidCC ?? 0m);
            var claimIds = userClaims.Select(c => c.ClaID);
            var financialSums = await _db.Service_Lines
                .Where(s => s.SrvClaFID.HasValue && claimIds.Contains(s.SrvClaFID.Value))
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalCharge = g.Sum(x => (decimal?)x.SrvCharges) ?? 0m,
                    TotalBalance = g.Sum(x => x.SrvTotalBalanceCC) ?? 0m
                })
                .FirstOrDefaultAsync();

            var statusData = await userClaims
                .GroupBy(c => string.IsNullOrWhiteSpace(c.ClaStatus) ? "Unknown" : c.ClaStatus!)
                .Select(g => new UserKpiStatusPointDto
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync();

            var trendRows = await userEditAudits
                .Where(a => a.ActivityDate >= trendStartUtc)
                .GroupBy(a => a.ActivityDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
            var trendMap = trendRows.ToDictionary(x => x.Date, x => x.Count);
            var trendData = new List<UserKpiTrendPointDto>(trendDays);
            for (var i = 0; i < trendDays; i++)
            {
                var day = trendStartUtc.AddDays(i).Date;
                trendData.Add(new UserKpiTrendPointDto
                {
                    Label = day.ToString("MMM dd"),
                    Value = trendMap.TryGetValue(day, out var count) ? count : 0
                });
            }

            var claimBalances = await (
                from c in userClaims
                join s in _db.Service_Lines.AsNoTracking() on c.ClaID equals s.SrvClaFID into sg
                select new
                {
                    c.ClaDateTimeCreated,
                    Balance = sg.Sum(x => x.SrvTotalBalanceCC) ?? 0m
                })
                .ToListAsync();

            decimal bucket0To30 = 0m;
            decimal bucket31To60 = 0m;
            decimal bucket61To90 = 0m;
            decimal bucket90Plus = 0m;
            foreach (var row in claimBalances)
            {
                if (row.Balance <= 0m) continue;
                var ageDays = (int)(nowUtc.Date - row.ClaDateTimeCreated.Date).TotalDays;
                if (ageDays <= 30) bucket0To30 += row.Balance;
                else if (ageDays <= 60) bucket31To60 += row.Balance;
                else if (ageDays <= 90) bucket61To90 += row.Balance;
                else bucket90Plus += row.Balance;
            }
            var agingData = new List<UserKpiAgingBucketDto>
            {
                new() { Label = "0-30", Value = bucket0To30 },
                new() { Label = "31-60", Value = bucket31To60 },
                new() { Label = "61-90", Value = bucket61To90 },
                new() { Label = "90+", Value = bucket90Plus }
            };

            var topPayers = await (
                from c in userClaims
                let payerName = _db.Patient_Insureds
                        .Where(pi => pi.PatInsPatFID == c.ClaPatFID && pi.PatInsSequence == 1)
                        .Select(pi => pi.PatInsIns.InsPay.PayName)
                        .FirstOrDefault()
                    ?? _db.Claim_Insureds
                        .Where(ci => ci.ClaInsClaFID == c.ClaID && ci.ClaInsSequence == 1)
                        .Select(ci => ci.ClaInsPayF.PayName)
                        .FirstOrDefault()
                    ?? "Unknown"
                group c by payerName into g
                orderby g.Count() descending
                select new UserKpiPayerPointDto
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .Take(5)
                .ToListAsync();

            return Ok(new UserKpiDashboardDto
            {
                UserName = userName,
                TotalClaims = totalClaims,
                TotalCharge = financialSums?.TotalCharge ?? 0m,
                TotalPaid = totalPaid,
                TotalBalance = financialSums?.TotalBalance ?? 0m,
                ClaimsByStatus = statusData,
                ClaimsTrend = trendData,
                AgingBuckets = agingData,
                TopPayers = topPayers
            });
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Claim"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        private static object? GetClaimColumnValue(Claim claimEntity, string key)
        {
            // Common UI aliases from Add Column registry to Claim entity members.
            var claimKey = key switch
            {
                "claFirstDOS" => "ClaFirstDateTRIG",
                "claLastDOS" => "ClaLastDateTRIG",
                "claPaidDate" => "ClaPaidDateTRIG",
                "claDischargeDate" => "ClaDischargedDate",
                "claDischargeHour" => "ClaDischargedHour",
                "claLastExported" => "ClaLastExportedDate",
                "claLastPrinted" => "ClaLastPrintedDate",
                "claCreatedTimestamp" => "ClaDateTimeCreated",
                "claModifiedTimestamp" => "ClaDateTimeModified",
                "claCreatedUser" => "ClaCreatedUserName",
                "claModifiedUser" => "ClaLastUserName",
                "claTotalInsAmtPaid" => "ClaTotalInsAmtPaidTRIG",
                "claTotalPatAmtPaid" => "ClaTotalPatAmtPaidTRIG",
                "claTotalCharge" => "ClaTotalChargeTRIG",
                "claTotalInsBalance" => "ClaTotalInsBalanceTRIG",
                "claTotalPatBalance" => "ClaTotalPatBalanceTRIG",
                "claVisitNumber" => "ClaMedicalRecordNumber",
                _ => key
            };

            if (string.Equals(key, "claActive", StringComparison.OrdinalIgnoreCase))
            {
                return !(claimEntity.ClaArchived ?? false);
            }

            var pascal = char.ToUpperInvariant(claimKey[0]) + claimKey.Substring(1);
            var prop = typeof(Claim).GetProperty(
                pascal,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);
            return prop?.GetValue(claimEntity);
        }

        /// <summary>
        /// Get claim notes from Claim_Audit (one row per note). Same data source as Claim Details Notes.
        /// Returns note fields + all claim list columns (claim + patient + additionalColumns).
        /// </summary>
        [HttpGet("notes")]
        public async Task<IActionResult> GetClaimNotes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] int? minClaimId = null,
            [FromQuery] int? maxClaimId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? searchText = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Page must be >= 1 and pageSize 1-100." });
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

            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Claim"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            var hasPatFirstName = columnsToInclude.Any(c => c.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(c => c.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(c => c.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(c => c.Key == "patAccountNo");
            var hasPatPhoneNo = columnsToInclude.Any(c => c.Key == "patPhoneNo");
            var hasPatCity = columnsToInclude.Any(c => c.Key == "patCity");
            var hasPatState = columnsToInclude.Any(c => c.Key == "patState");
            var hasPatBirthDate = columnsToInclude.Any(c => c.Key == "patBirthDate");
            var hasRenderingPhyName = columnsToInclude.Any(c => c.Key == "renderingPhyName");
            var hasRenderingPhyNPI = columnsToInclude.Any(c => c.Key == "renderingPhyNPI");
            var hasBillingPhyName = columnsToInclude.Any(c => c.Key == "billingPhyName");
            var hasBillingPhyNPI = columnsToInclude.Any(c => c.Key == "billingPhyNPI");
            var hasFacilityName = columnsToInclude.Any(c => c.Key == "facilityName");

            try
            {
                // Use LEFT JOINs for Patient and Physicians so claims with FK=0 or missing refs still appear
                var query = from a in _db.Claim_Audits.AsNoTracking()
                           join c in _db.Claims.AsNoTracking() on a.ClaFID equals c.ClaID
                           join p in _db.Patients.AsNoTracking() on c.ClaPatFID equals p.PatID into patientGroup
                           from p in patientGroup.DefaultIfEmpty()
                           join rend in _db.Physicians.AsNoTracking() on c.ClaRenderingPhyFID equals rend.PhyID into rendGrp
                           from rend in rendGrp.DefaultIfEmpty()
                           join bill in _db.Physicians.AsNoTracking() on c.ClaBillingPhyFID equals bill.PhyID into billGrp
                           from bill in billGrp.DefaultIfEmpty()
                           join fac in _db.Physicians.AsNoTracking() on c.ClaFacilityPhyFID equals fac.PhyID into facGrp
                           from fac in facGrp.DefaultIfEmpty()
                           select new
                           {
                               a.AuditID,
                               ClaID = a.ClaFID,
                               a.ActivityDate,
                               a.UserName,
                               a.Notes,
                               a.ActivityType,
                               a.TotalCharge,
                               a.InsuranceBalance,
                               a.PatientBalance,
                               // Claim fields (all claim list columns)
                               c.ClaStatus,
                               c.ClaDateTimeCreated,
                               c.ClaDateTimeModified,
                               c.ClaTotalChargeTRIG,
                               c.ClaTotalBalanceCC,
                               c.ClaClassification,
                               c.ClaFirstDateTRIG,
                               c.ClaLastDateTRIG,
                               c.ClaBillDate,
                               c.ClaBillTo,
                               c.ClaPatFID,
                               c.ClaTypeOfBill,
                               c.ClaAdmissionType,
                               c.ClaPatientStatus,
                               c.ClaDiagnosis1,
                               c.ClaDiagnosis2,
                               c.ClaDiagnosis3,
                               c.ClaDiagnosis4,
                               c.ClaLastUserName,
                               c.ClaRenderingPhyFID,
                               c.ClaBillingPhyFID,
                               c.ClaFacilityPhyFID,
                               // Patient
                               PatFullNameCC = p != null ? p.PatFullNameCC : null,
                               PatFirstName = p != null ? p.PatFirstName : null,
                               PatLastName = p != null ? p.PatLastName : null,
                               PatAccountNo = p != null ? p.PatAccountNo : null,
                               PatPhoneNo = p != null ? p.PatPhoneNo : null,
                               PatCity = p != null ? p.PatCity : null,
                               PatState = p != null ? p.PatState : null,
                               PatBirthDate = p != null ? p.PatBirthDate : (DateOnly?)null,
                               // Physicians (LEFT JOIN - avoids excluding rows when FK=0 or missing)
                               RenderingPhyName = rend != null ? rend.PhyName : null,
                               RenderingPhyNPI = rend != null ? rend.PhyNPI : null,
                               BillingPhyName = bill != null ? bill.PhyName : null,
                               BillingPhyNPI = bill != null ? bill.PhyNPI : null,
                               FacilityPhyName = fac != null ? fac.PhyName : null
                           };

                if (minClaimId.HasValue)
                    query = query.Where(x => x.ClaID >= minClaimId.Value);
                if (maxClaimId.HasValue)
                    query = query.Where(x => x.ClaID <= maxClaimId.Value);
                if (fromDate.HasValue)
                    query = query.Where(x => x.ActivityDate >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var endOfDay = toDate.Value.Date.AddDays(1);
                    query = query.Where(x => x.ActivityDate < endOfDay);
                }
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var q = searchText.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Notes != null && x.Notes.ToLower().Contains(q)) ||
                        (x.ActivityType != null && x.ActivityType.ToLower().Contains(q)) ||
                        (x.UserName != null && x.UserName.ToLower().Contains(q)));
                }

                var totalCount = await query.CountAsync();
                var data = await query
                    .OrderByDescending(x => x.ActivityDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = data.Select(x =>
                {
                    var noteText = !string.IsNullOrWhiteSpace(x.Notes) ? x.Notes : x.ActivityType;
                    var patientName = x.PatFullNameCC ?? (string.IsNullOrEmpty(x.PatFirstName) && string.IsNullOrEmpty(x.PatLastName) ? null : (x.PatFirstName + " " + x.PatLastName).Trim());
                    var addCols = new Dictionary<string, object?>();
                    if (hasPatFirstName) addCols["patFirstName"] = x.PatFirstName;
                    if (hasPatLastName) addCols["patLastName"] = x.PatLastName;
                    if (hasPatFullNameCC) addCols["patFullNameCC"] = x.PatFullNameCC;
                    if (hasPatAccountNo) addCols["patAccountNo"] = x.PatAccountNo;
                    if (hasPatPhoneNo) addCols["patPhoneNo"] = x.PatPhoneNo;
                    if (hasPatCity) addCols["patCity"] = x.PatCity;
                    if (hasPatState) addCols["patState"] = x.PatState;
                    if (hasPatBirthDate) addCols["patBirthDate"] = x.PatBirthDate;
                    if (hasRenderingPhyName) addCols["renderingPhyName"] = x.RenderingPhyName;
                    if (hasRenderingPhyNPI) addCols["renderingPhyNPI"] = x.RenderingPhyNPI;
                    if (hasBillingPhyName) addCols["billingPhyName"] = x.BillingPhyName;
                    if (hasBillingPhyNPI) addCols["billingPhyNPI"] = x.BillingPhyNPI;
                    if (hasFacilityName) addCols["facilityName"] = x.FacilityPhyName;

                    return new
                    {
                        x.AuditID,
                        x.ClaID,
                        activityDate = x.ActivityDate,
                        userName = x.UserName ?? "SYSTEM",
                        noteText,
                        x.TotalCharge,
                        x.InsuranceBalance,
                        x.PatientBalance,
                        patientName,
                        // Claim list columns
                        claStatus = x.ClaStatus,
                        claDateTimeCreated = x.ClaDateTimeCreated,
                        claDateTimeModified = x.ClaDateTimeModified,
                        claTotalChargeTRIG = x.ClaTotalChargeTRIG,
                        claTotalBalanceCC = x.ClaTotalBalanceCC,
                        claClassification = x.ClaClassification,
                        claFirstDateTRIG = x.ClaFirstDateTRIG,
                        claLastDateTRIG = x.ClaLastDateTRIG,
                        claBillDate = x.ClaBillDate,
                        claBillTo = x.ClaBillTo,
                        claPatFID = x.ClaPatFID,
                        claTypeOfBill = x.ClaTypeOfBill,
                        claAdmissionType = x.ClaAdmissionType,
                        claPatientStatus = x.ClaPatientStatus,
                        claDiagnosis1 = x.ClaDiagnosis1,
                        claDiagnosis2 = x.ClaDiagnosis2,
                        claDiagnosis3 = x.ClaDiagnosis3,
                        claDiagnosis4 = x.ClaDiagnosis4,
                        claLastUserName = x.ClaLastUserName,
                        patFullNameCC = x.PatFullNameCC,
                        patFirstName = x.PatFirstName,
                        patLastName = x.PatLastName,
                        patAccountNo = x.PatAccountNo,
                        additionalColumns = addCols
                    };
                }).ToList();

                return Ok(new
                {
                    data = items,
                    meta = new { page, pageSize, totalCount }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Claim_Audit/GetClaimNotes failed. Table may not exist.");
                return Ok(new { data = Array.Empty<object>(), meta = new { page, pageSize, totalCount = 0 } });
            }
        }

        /// <summary>
        /// Generates 837 for the claim (Payer rules), updates status to Submitted, returns 837 content.
        /// </summary>
        [HttpPost("{claId:int}/export837")]
        public async Task<IActionResult> Export837([FromRoute] int claId)
        {
            if (claId <= 0) return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Claim ID must be greater than 0" });
            try
            {
                // Load claim settings
                var settingsElement = await _programSettingsService.GetSectionAsync("claim", HttpContext.RequestAborted);
                var claimSettings = settingsElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    ? settingsElement
                    : System.Text.Json.JsonDocument.Parse("{}").RootElement;

                bool lockClaimsAfterPrint = false;
                if (claimSettings.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    claimSettings.TryGetProperty("lockClaimsAfterPrint", out var lockProp) &&
                    lockProp.ValueKind == System.Text.Json.JsonValueKind.True)
                {
                    lockClaimsAfterPrint = true;
                }

                bool checkDuplicateServiceLines = false;
                if (claimSettings.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    claimSettings.TryGetProperty("checkDuplicateServiceLines", out var dupProp) &&
                    dupProp.ValueKind == System.Text.Json.JsonValueKind.True)
                {
                    checkDuplicateServiceLines = true;
                }

                bool validateIcdLogic = false;
                if (claimSettings.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    claimSettings.TryGetProperty("validateICDLogic", out var icdProp) &&
                    icdProp.ValueKind == System.Text.Json.JsonValueKind.True)
                {
                    validateIcdLogic = true;
                }

                // Load claim and service lines for validation
                var claim = await _db.Claims
                    .Include(c => c.Service_Lines)
                    .FirstOrDefaultAsync(c => c.ClaID == claId);

                if (claim == null)
                    return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Claim not found." });

                if (checkDuplicateServiceLines)
                {
                    var duplicates = claim.Service_Lines
                        .GroupBy(s => new
                        {
                            s.SrvFromDate,
                            s.SrvProcedureCode,
                            s.SrvProductCode,
                            s.SrvModifier1,
                            s.SrvModifier2,
                            s.SrvModifier3,
                            s.SrvModifier4,
                            s.SrvDiagnosisPointer
                        })
                        .Where(g => g.Count() > 1)
                        .FirstOrDefault();

                    if (duplicates != null)
                    {
                        return BadRequest(new ErrorResponseDto
                        {
                            ErrorCode = "CLAIM_VALIDATION",
                            Message = "Duplicate service lines detected. Please remove duplicates before exporting."
                        });
                    }
                }

                if (validateIcdLogic)
                {
                    var serviceDates = claim.Service_Lines
                        .Select(s => s.SrvFromDate)
                        .OrderBy(d => d)
                        .ToList();

                    if (serviceDates.Count > 0)
                    {
                        var cutoff = new DateOnly(2015, 10, 1);
                        bool hasBefore = serviceDates.Any(d => d < cutoff);
                        bool hasAfterOrOn = serviceDates.Any(d => d >= cutoff);

                        if (hasBefore && hasAfterOrOn)
                        {
                            return BadRequest(new ErrorResponseDto
                            {
                                ErrorCode = "CLAIM_VALIDATION",
                                Message = "Cannot combine service dates before and after Oct 1 2015 on the same claim."
                            });
                        }

                        var icdIndicator = claim.ClaICDIndicator?.Trim();
                        if (string.IsNullOrEmpty(icdIndicator))
                        {
                            return BadRequest(new ErrorResponseDto
                            {
                                ErrorCode = "CLAIM_VALIDATION",
                                Message = "ICD Indicator is required."
                            });
                        }

                        if (hasBefore && icdIndicator == "0")
                        {
                            return BadRequest(new ErrorResponseDto
                            {
                                ErrorCode = "CLAIM_VALIDATION",
                                Message = "For service dates before Oct 1 2015, ICD Indicator cannot be 0 (ICD-10)."
                            });
                        }

                        if (hasAfterOrOn && icdIndicator == "9")
                        {
                            return BadRequest(new ErrorResponseDto
                            {
                                ErrorCode = "CLAIM_VALIDATION",
                                Message = "For service dates on or after Oct 1 2015, ICD Indicator cannot be 9 (ICD-9)."
                            });
                        }

                        var diagnoses = new[]
                        {
                            claim.ClaDiagnosis1,
                            claim.ClaDiagnosis2,
                            claim.ClaDiagnosis3,
                            claim.ClaDiagnosis4,
                            claim.ClaDiagnosis5
                        }.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

                        if (diagnoses.Count > 0)
                        {
                            foreach (var dx in diagnoses)
                            {
                                var trimmed = dx!.Trim();
                                if (string.IsNullOrEmpty(trimmed))
                                    continue;

                                var first = trimmed[0];
                                if (icdIndicator == "0")
                                {
                                    if (!char.IsLetter(first))
                                    {
                                        return BadRequest(new ErrorResponseDto
                                        {
                                            ErrorCode = "CLAIM_VALIDATION",
                                            Message = $"ICD-10 diagnosis '{trimmed}' must start with a letter."
                                        });
                                    }
                                }
                                else if (icdIndicator == "9")
                                {
                                    if (char.IsLetter(first) && char.ToUpperInvariant(first) != 'E' && char.ToUpperInvariant(first) != 'V')
                                    {
                                        return BadRequest(new ErrorResponseDto
                                        {
                                            ErrorCode = "CLAIM_VALIDATION",
                                            Message = $"ICD-9 diagnosis '{trimmed}' must start with a number, or E/V."
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                var content = await _claimExportService.Generate837Async(claId);

                if (lockClaimsAfterPrint)
                {
                    claim.ClaLocked = true;
                    await _db.SaveChangesAsync();
                }

                return Ok(new { content });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "EXPORT_RULE", Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export 837 failed for claim {ClaId}", claId);
                return StatusCode(500, new ErrorResponseDto { ErrorCode = "EXPORT_ERROR", Message = ex.Message });
            }
        }

        [HttpGet("{claId:int}")]
        public async Task<IActionResult> GetClaimById([FromRoute] int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Claim ID must be greater than 0"
                });
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var claimHeader = await _db.Claims
                    .AsNoTracking()
                    .Where(c => c.ClaID == claId)
                    .Select(c => new
                    {
                        c.ClaID,
                        c.ClaPatFID,
                        c.ClaStatus,
                        ClaDateTimeCreated = c.ClaDateTimeCreated,
                        ClaDateTimeModified = c.ClaDateTimeModified,
                        c.ClaTotalChargeTRIG,
                        c.ClaTotalAmtPaidCC,
                        c.ClaTotalBalanceCC,
                        c.ClaTotalAmtAppliedCC,
                        ClaBillDate = c.ClaBillDate.HasValue ? c.ClaBillDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaBillTo,
                        c.ClaSubmissionMethod,
                        c.ClaInvoiceNumber,
                        c.ClaLocked,
                        c.ClaOriginalRefNo,
                        c.ClaDelayCode,
                        c.ClaMedicaidResubmissionCode,
                        c.ClaPaperWorkTransmissionCode,
                        c.ClaPaperWorkControlNumber,
                        c.ClaPaperWorkInd,
                        c.ClaEDINotes,
                        c.ClaRemarks,
                        ClaAdmittedDate = c.ClaAdmittedDate.HasValue ? c.ClaAdmittedDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaDischargedDate = c.ClaDischargedDate.HasValue ? c.ClaDischargedDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaDateLastSeen = c.ClaDateLastSeen.HasValue ? c.ClaDateLastSeen.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaRelatedTo,
                        c.ClaRelatedToState,
                        ClaFirstDateTRIG = c.ClaFirstDateTRIG.HasValue ? c.ClaFirstDateTRIG.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        ClaLastDateTRIG = c.ClaLastDateTRIG.HasValue ? c.ClaLastDateTRIG.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        c.ClaClassification,
                        c.ClaDiagnosis1,
                        c.ClaDiagnosis2,
                        c.ClaDiagnosis3,
                        c.ClaDiagnosis4,
                        c.ClaDiagnosis5,
                        c.ClaDiagnosis6,
                        c.ClaDiagnosis7,
                        c.ClaDiagnosis8,
                        c.ClaDiagnosis9,
                        c.ClaDiagnosis10,
                        c.ClaDiagnosis11,
                        c.ClaDiagnosis12,
                        Patient = c.ClaPatF == null ? null : new
                        {
                            c.ClaPatF.PatID,
                            c.ClaPatF.PatFirstName,
                            c.ClaPatF.PatLastName,
                            c.ClaPatF.PatFullNameCC,
                            PatBirthDate = c.ClaPatF.PatBirthDate.HasValue ? c.ClaPatF.PatBirthDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                            c.ClaPatF.PatAccountNo,
                            c.ClaPatF.PatPhoneNo,
                            c.ClaPatF.PatCity,
                            c.ClaPatF.PatState
                        },
                        RenderingPhysician = c.ClaRenderingPhyF == null ? null : new { c.ClaRenderingPhyF.PhyID, c.ClaRenderingPhyF.PhyName, c.ClaRenderingPhyF.PhyNPI },
                        ReferringPhysician = c.ClaReferringPhyF == null ? null : new { c.ClaReferringPhyF.PhyID, c.ClaReferringPhyF.PhyName, c.ClaReferringPhyF.PhyNPI },
                        BillingPhysician = c.ClaBillingPhyF == null ? null : new { c.ClaBillingPhyF.PhyID, c.ClaBillingPhyF.PhyName, c.ClaBillingPhyF.PhyNPI },
                        FacilityPhysician = c.ClaFacilityPhyF == null ? null : new { c.ClaFacilityPhyF.PhyID, c.ClaFacilityPhyF.PhyName, c.ClaFacilityPhyF.PhyNPI },
                    })
                    .FirstOrDefaultAsync(cts.Token);

                if (claimHeader == null) return NotFound();

                var claimInsured = await _db.Claim_Insureds
                    .AsNoTracking()
                    .Where(ci => ci.ClaInsClaFID == claId)
                    .Select(ci => new
                    {
                        ci.ClaInsGUID,
                        ci.ClaInsSequence,
                        ci.ClaInsPayFID,
                        PayerName = ci.ClaInsPayF != null ? ci.ClaInsPayF.PayName : null
                    })
                    .ToListAsync(cts.Token);

                var serviceLineRows = await _db.Service_Lines
                    .AsNoTracking()
                    .Where(s => s.SrvClaFID == claId)
                    .OrderBy(s => s.SrvID)
                    .Select(s => new
                    {
                        s.SrvID,
                        SrvFromDate = s.SrvFromDate != default(DateOnly) ? s.SrvFromDate.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        SrvToDate = s.SrvToDate != default(DateOnly) ? s.SrvToDate.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvPlace,
                        s.SrvDiagnosisPointer,
                        s.SrvTotalInsAmtPaidTRIG,
                        s.SrvTotalPatAmtPaidTRIG,
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        s.SrvTotalAdjCC,
                        s.SrvTotalAmtAppliedCC,
                        s.SrvResponsibleParty,
                        ResponsiblePartyName = s.SrvResponsibleParty == 0
                            ? "Patient"
                            : (s.SrvResponsiblePartyNavigation != null ? s.SrvResponsiblePartyNavigation.PayName : null)
                    })
                    .ToListAsync(cts.Token);

                var serviceLineIds = serviceLineRows.Select(s => s.SrvID).ToList();

                var adjustmentRows = serviceLineIds.Count == 0
                    ? new List<(int AdjSrvFID, int AdjID, DateTime? AdjDate, decimal AdjAmount, string AdjGroupCode, string? AdjReasonCode, DateTime AdjDateTimeCreated, string? PayerName)>()
                    : (await _db.Adjustments
                        .AsNoTracking()
                        .Where(a => serviceLineIds.Contains(a.AdjSrvFID))
                        .Select(a => new
                        {
                            a.AdjID,
                            a.AdjSrvFID,
                            AdjDate = a.AdjDate.HasValue ? a.AdjDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                            a.AdjAmount,
                            a.AdjGroupCode,
                            a.AdjReasonCode,
                            AdjDateTimeCreated = a.AdjDateTimeCreated,
                            PayerName = a.AdjPayF != null ? a.AdjPayF.PayName : null
                        })
                        .ToListAsync(cts.Token))
                        .Select(a => (
                            AdjSrvFID: a.AdjSrvFID,
                            AdjID: a.AdjID,
                            AdjDate: a.AdjDate,
                            AdjAmount: a.AdjAmount,
                            AdjGroupCode: a.AdjGroupCode ?? string.Empty,
                            AdjReasonCode: a.AdjReasonCode,
                            AdjDateTimeCreated: a.AdjDateTimeCreated,
                            PayerName: a.PayerName))
                        .ToList();

                var disbursementRows = serviceLineIds.Count == 0
                    ? new List<(int DisbSrvFID, int DisbID, decimal DisbAmount, DateTime DisbDateTimeCreated, object? Payment)>()
                    : (await _db.Disbursements
                        .AsNoTracking()
                        .Where(d => serviceLineIds.Contains(d.DisbSrvFID))
                        .Select(d => new
                        {
                            d.DisbID,
                            d.DisbSrvFID,
                            d.DisbAmount,
                            d.DisbDateTimeCreated,
                            Payment = d.DisbPmtF == null ? null : new
                            {
                                d.DisbPmtF.PmtID,
                                PmtDate = d.DisbPmtF.PmtDate.ToDateTime(TimeOnly.MinValue),
                                d.DisbPmtF.PmtAmount,
                                d.DisbPmtF.PmtMethod,
                                d.DisbPmtF.Pmt835Ref,
                                d.DisbPmtF.PmtDateTimeCreated
                            }
                        })
                        .ToListAsync(cts.Token))
                        .Select(d => (
                            DisbSrvFID: d.DisbSrvFID,
                            DisbID: d.DisbID,
                            DisbAmount: d.DisbAmount,
                            DisbDateTimeCreated: d.DisbDateTimeCreated,
                            Payment: (object?)d.Payment))
                        .ToList();

                var claimActivity = await _db.Claim_Audits
                    .AsNoTracking()
                    .Where(a => a.ClaFID == claId)
                    .OrderByDescending(a => a.ActivityDate)
                    .Take(50)
                    .Select(a => new
                    {
                        date = a.ActivityDate,
                        user = a.UserName ?? "SYSTEM",
                        activityType = a.ActivityType,
                        notes = a.Notes,
                        totalCharge = a.TotalCharge,
                        insuranceBalance = a.InsuranceBalance,
                        patientBalance = a.PatientBalance
                    })
                    .ToListAsync(cts.Token);

                var adjustmentsByServiceLine = adjustmentRows
                    .GroupBy(a => a.AdjSrvFID)
                    .ToDictionary(g => g.Key, g => g.Select(a => (object)new
                    {
                        a.AdjID,
                        a.AdjDate,
                        a.AdjAmount,
                        a.AdjGroupCode,
                        a.AdjReasonCode,
                        a.AdjDateTimeCreated,
                        a.PayerName
                    }).ToList());

                var disbursementsByServiceLine = disbursementRows
                    .GroupBy(d => d.DisbSrvFID)
                    .ToDictionary(g => g.Key, g => g.Select(d => (object)new
                    {
                        d.DisbID,
                        d.DisbAmount,
                        d.DisbDateTimeCreated,
                        d.Payment
                    }).ToList());

                var serviceLines = serviceLineRows.Select(s => new
                {
                    s.SrvID,
                    s.SrvFromDate,
                    s.SrvToDate,
                    s.SrvProcedureCode,
                    s.SrvDesc,
                    s.SrvCharges,
                    s.SrvUnits,
                    s.SrvPlace,
                    s.SrvDiagnosisPointer,
                    s.SrvTotalInsAmtPaidTRIG,
                    s.SrvTotalPatAmtPaidTRIG,
                    s.SrvTotalBalanceCC,
                    s.SrvTotalAmtPaidCC,
                    s.SrvTotalAdjCC,
                    s.SrvTotalAmtAppliedCC,
                    s.SrvResponsibleParty,
                    s.ResponsiblePartyName,
                    Adjustments = adjustmentsByServiceLine.TryGetValue(s.SrvID, out var adj) ? adj : new List<object>(),
                    Disbursements = disbursementsByServiceLine.TryGetValue(s.SrvID, out var disb) ? disb : new List<object>()
                }).ToList();

                var claim = new
                {
                    claimHeader.ClaID,
                    claimHeader.ClaPatFID,
                    claimHeader.ClaStatus,
                    claimHeader.ClaDateTimeCreated,
                    claimHeader.ClaDateTimeModified,
                    claimHeader.ClaTotalChargeTRIG,
                    claimHeader.ClaTotalAmtPaidCC,
                    claimHeader.ClaTotalBalanceCC,
                    claimHeader.ClaTotalAmtAppliedCC,
                    claimHeader.ClaBillDate,
                    claimHeader.ClaBillTo,
                    claimHeader.ClaSubmissionMethod,
                    claimHeader.ClaInvoiceNumber,
                    claimHeader.ClaLocked,
                    claimHeader.ClaOriginalRefNo,
                    claimHeader.ClaDelayCode,
                    claimHeader.ClaMedicaidResubmissionCode,
                    claimHeader.ClaPaperWorkTransmissionCode,
                    claimHeader.ClaPaperWorkControlNumber,
                    claimHeader.ClaPaperWorkInd,
                    claimHeader.ClaEDINotes,
                    claimHeader.ClaRemarks,
                    claimHeader.ClaAdmittedDate,
                    claimHeader.ClaDischargedDate,
                    claimHeader.ClaDateLastSeen,
                    claimHeader.ClaRelatedTo,
                    claimHeader.ClaRelatedToState,
                    claimHeader.ClaFirstDateTRIG,
                    claimHeader.ClaLastDateTRIG,
                    claimHeader.ClaClassification,
                    claimHeader.ClaDiagnosis1,
                    claimHeader.ClaDiagnosis2,
                    claimHeader.ClaDiagnosis3,
                    claimHeader.ClaDiagnosis4,
                    claimHeader.ClaDiagnosis5,
                    claimHeader.ClaDiagnosis6,
                    claimHeader.ClaDiagnosis7,
                    claimHeader.ClaDiagnosis8,
                    claimHeader.ClaDiagnosis9,
                    claimHeader.ClaDiagnosis10,
                    claimHeader.ClaDiagnosis11,
                    claimHeader.ClaDiagnosis12,
                    claimHeader.Patient,
                    claimHeader.RenderingPhysician,
                    claimHeader.ReferringPhysician,
                    claimHeader.BillingPhysician,
                    claimHeader.FacilityPhysician,
                    ClaimInsured = claimInsured,
                    ServiceLines = serviceLines,
                    ClaimActivity = claimActivity
                };

                return Ok(claim);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetClaimById query timed out for claim ID: {ClaId}", claId);
                return StatusCode(503, new ErrorResponseDto
                {
                    ErrorCode = "QUERY_TIMEOUT",
                    Message = "The query took too long to execute. Please try again or contact support if the issue persists."
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
            {
                _logger.LogWarning(sqlEx, "GetClaimById SQL timeout for claim ID: {ClaId}", claId);
                return StatusCode(503, new ErrorResponseDto
                {
                    ErrorCode = "QUERY_TIMEOUT",
                    Message = "The query took too long to execute. Please try again or contact support if the issue persists."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting claim by ID: {ClaId}", claId);
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "INTERNAL_ERROR",
                    Message = "An error occurred while retrieving the claim details."
                });
            }
        }

        /// <summary>
        /// Update claim fields. ClaClassification (Facility) values come from Libraries → List → Claim Classification.
        /// </summary>
        [HttpPut("{claId:int}")]
        public async Task<IActionResult> UpdateClaim([FromRoute] int claId, [FromBody] Zebl.Application.Dtos.Claims.UpdateClaimRequest request)
        {
            if (claId <= 0)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Invalid claim ID" });
            if (request == null)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Request body is required" });

            if (!string.IsNullOrWhiteSpace(request.ClaStatus) && !ClaimStatusCatalog.IsValidStoredValue(request.ClaStatus))
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Invalid claim status." });
            }

            try
            {
                var claim = await _db.Claims.FindAsync(claId);
                if (claim == null)
                    return NotFound();

                claim.ClaClassification = request.ClaClassification != null
                    ? (request.ClaClassification.Length > 30 ? request.ClaClassification[..30] : request.ClaClassification)
                    : null;
                if (!string.IsNullOrWhiteSpace(request.ClaStatus))
                {
                    claim.ClaStatus = request.ClaStatus;
                }
                claim.ClaSubmissionMethod = request.ClaSubmissionMethod;
                if (request.ClaRenderingPhyFID.HasValue)
                    claim.ClaRenderingPhyFID = request.ClaRenderingPhyFID.Value;
                if (request.ClaFacilityPhyFID.HasValue)
                    claim.ClaFacilityPhyFID = request.ClaFacilityPhyFID.Value;
                claim.ClaInvoiceNumber = request.ClaInvoiceNumber;
                if (request.ClaAdmittedDate.HasValue)
                    claim.ClaAdmittedDate = DateOnly.FromDateTime(request.ClaAdmittedDate.Value);
                if (request.ClaDischargedDate.HasValue)
                    claim.ClaDischargedDate = DateOnly.FromDateTime(request.ClaDischargedDate.Value);
                if (request.ClaDateLastSeen.HasValue)
                    claim.ClaDateLastSeen = DateOnly.FromDateTime(request.ClaDateLastSeen.Value);
                if (request.ClaEDINotes != null)
                    claim.ClaEDINotes = request.ClaEDINotes;
                if (request.ClaRemarks != null)
                    claim.ClaRemarks = request.ClaRemarks;
                if (request.ClaRelatedTo.HasValue)
                {
                    // Ensure the int value fits into the target short? property to avoid data loss
                    if (request.ClaRelatedTo.Value < short.MinValue || request.ClaRelatedTo.Value > short.MaxValue)
                    {
                        return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "ClaRelatedTo value is out of range" });
                    }

                    claim.ClaRelatedTo = (short?)request.ClaRelatedTo.Value;
                }
                if (request.ClaRelatedToState != null)
                    claim.ClaRelatedToState = request.ClaRelatedToState;
                if (request.ClaLocked.HasValue)
                    claim.ClaLocked = request.ClaLocked.Value;
                claim.ClaDelayCode = request.ClaDelayCode != null && request.ClaDelayCode.Length > 2 ? request.ClaDelayCode[..2] : request.ClaDelayCode;
                claim.ClaMedicaidResubmissionCode = request.ClaMedicaidResubmissionCode != null && request.ClaMedicaidResubmissionCode.Length > 50 ? request.ClaMedicaidResubmissionCode[..50] : request.ClaMedicaidResubmissionCode;
                claim.ClaOriginalRefNo = request.ClaOriginalRefNo != null && request.ClaOriginalRefNo.Length > 80 ? request.ClaOriginalRefNo[..80] : request.ClaOriginalRefNo;
                claim.ClaPaperWorkTransmissionCode = request.ClaPaperWorkTransmissionCode != null && request.ClaPaperWorkTransmissionCode.Length > 2 ? request.ClaPaperWorkTransmissionCode[..2] : request.ClaPaperWorkTransmissionCode;
                claim.ClaPaperWorkControlNumber = request.ClaPaperWorkControlNumber != null && request.ClaPaperWorkControlNumber.Length > 80 ? request.ClaPaperWorkControlNumber[..80] : request.ClaPaperWorkControlNumber;
                claim.ClaPaperWorkInd = request.ClaPaperWorkInd != null && request.ClaPaperWorkInd.Length > 20 ? request.ClaPaperWorkInd[..20] : request.ClaPaperWorkInd;

                if (request.AdditionalData != null)
                {
                    claim.ClaAdditionalData = SerializeClaimAdditionalData(request.AdditionalData);
                }

                var serviceLineSnapshot = await _db.Service_Lines
                    .AsNoTracking()
                    .Where(s => s.SrvClaFID == claId)
                    .OrderBy(s => s.SrvID)
                    .Select(s => new { s.SrvID, s.SrvProcedureCode })
                    .ToListAsync();
                var serviceLineIds = serviceLineSnapshot.Select(s => s.SrvID.ToString()).ToList();
                var serviceLineProcCodes = serviceLineSnapshot.Select(s => $"{s.SrvID}:{s.SrvProcedureCode}").ToList();

                _logger.LogInformation(
                    "Updating claim {ClaId}. Request snapshot: Status={Status}, SubmissionMethod={SubmissionMethod}, RenderingPhy={RenderingPhy}, FacilityPhy={FacilityPhy}, BillingPhy={BillingPhy}, Locked={Locked}, ServiceLineIds={ServiceLineIds}, ServiceLineProcs={ServiceLineProcs}",
                    claId,
                    request.ClaStatus,
                    request.ClaSubmissionMethod,
                    claim.ClaRenderingPhyFID,
                    claim.ClaFacilityPhyFID,
                    claim.ClaBillingPhyFID,
                    request.ClaLocked,
                    string.Join(",", serviceLineIds),
                    string.Join(",", serviceLineProcCodes));

                // Prepare claim audit before save so the request performs a single SaveChangesAsync().
                var userName = _userContext.UserName ?? "SYSTEM";
                var computerName = _userContext.ComputerName ?? Environment.MachineName;
                var noteText = !string.IsNullOrWhiteSpace(request.NoteText)
                    ? request.NoteText.Trim().Length > 500 ? request.NoteText.Trim()[..500] : request.NoteText.Trim()
                    : "Claim edited.";
                _db.Claim_Audits.Add(new Claim_Audit
                {
                    ClaFID = claId,
                    ActivityType = "Claim Edited",
                    ActivityDate = DateTime.UtcNow,
                    UserName = userName,
                    ComputerName = computerName,
                    Notes = noteText,
                    TotalCharge = claim.ClaTotalChargeTRIG,
                    InsuranceBalance = claim.ClaTotalInsBalanceTRIG,
                    PatientBalance = claim.ClaTotalPatBalanceTRIG
                });

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    var inner = dbEx.InnerException?.Message;
                    var trackedEntries = string.Join(", ", dbEx.Entries.Select(e => e.Entity.GetType().Name));
                    _logger.LogError(
                        dbEx,
                        "DbUpdateException while updating claim {ClaId}. Tracked entities: {TrackedEntries}. Inner: {InnerMessage}",
                        claId,
                        trackedEntries,
                        inner);
                    _logger.LogError("SQL error: {Message}", dbEx.InnerException?.Message);
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "FK_VALIDATION",
                        Message = "Claim update failed due to invalid related data (e.g., physician reference)."
                    });
                }

                _logger.LogInformation("Updated claim {ClaId}, ClaClassification={ClaClassification}", claId, claim.ClaClassification);
                return Ok(new
                {
                    claim.ClaID,
                    claim.ClaTotalChargeTRIG,
                    claim.ClaTotalAmtPaidCC,
                    claim.ClaTotalBalanceCC,
                    claim.ClaTotalAmtAppliedCC
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating claim {ClaId}. Payload summary: Status={Status}, SubmissionMethod={SubmissionMethod}, RenderingPhy={RenderingPhy}, FacilityPhy={FacilityPhy}, RelatedTo={RelatedTo}",
                    claId,
                    request?.ClaStatus,
                    request?.ClaSubmissionMethod,
                    request?.ClaRenderingPhyFID,
                    request?.ClaFacilityPhyFID,
                    request?.ClaRelatedTo);
                return StatusCode(500, new ErrorResponseDto { ErrorCode = "INTERNAL_ERROR", Message = "Failed to update claim" });
            }
        }

        private static ClaimAdditionalData? DeserializeClaimAdditionalData(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(ClaimAdditionalData));
                using var reader = new StringReader(xml.Trim());
                return (ClaimAdditionalData?)serializer.Deserialize(reader);
            }
            catch
            {
                return null;
            }
        }

        private static string? SerializeClaimAdditionalData(ClaimAdditionalData data)
        {
            if (data == null) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(ClaimAdditionalData));
                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb))
                {
                    serializer.Serialize(writer, data);
                }
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task<int> GetApproxClaimCountAsync()
        {
            try
            {
                // Fast metadata-based row count (works well for large tables)
                // index_id 0 = heap, 1 = clustered index
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Claim]')
    AND p.index_id IN (0, 1)";

                return await _db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            }
            catch
            {
                // Fallback: if sys.* access is restricted, don't break the endpoint
                return 0;
            }
        }
    }




}


