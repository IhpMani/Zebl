using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Zebl.Application.Abstractions;
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

        public ClaimsController(ZeblDbContext db, ICurrentUserContext userContext, IClaimExportService claimExportService, ISecondaryTriggerService secondaryTriggerService, ILogger<ClaimsController> logger)
        {
            _db = db;
            _userContext = userContext;
            _claimExportService = claimExportService;
            _secondaryTriggerService = secondaryTriggerService;
            _logger = logger;
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

            // Get available column definitions
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Claim"];
            var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();
            
            // Pre-evaluate which columns are requested to avoid evaluating in Select()
            var hasPatFirstName = columnsToInclude.Any(col => col.Key == "patFirstName");
            var hasPatLastName = columnsToInclude.Any(col => col.Key == "patLastName");
            var hasPatFullNameCC = columnsToInclude.Any(col => col.Key == "patFullNameCC");
            var hasPatAccountNo = columnsToInclude.Any(col => col.Key == "patAccountNo");
            var hasPatPhoneNo = columnsToInclude.Any(col => col.Key == "patPhoneNo");
            var hasPatCity = columnsToInclude.Any(col => col.Key == "patCity");
            var hasPatState = columnsToInclude.Any(col => col.Key == "patState");
            var hasPatBirthDate = columnsToInclude.Any(col => col.Key == "patBirthDate");
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
                query = query.Where(c => c.ClaTotalChargeTRIG >= minTotalCharge.Value);
            }

            if (maxTotalCharge.HasValue)
            {
                query = query.Where(c => c.ClaTotalChargeTRIG <= maxTotalCharge.Value);
            }

            // Total Balance range filter
            if (minTotalBalance.HasValue)
            {
                query = query.Where(c => c.ClaTotalBalanceCC.HasValue && c.ClaTotalBalanceCC >= minTotalBalance.Value);
            }

            if (maxTotalBalance.HasValue)
            {
                query = query.Where(c => c.ClaTotalBalanceCC.HasValue && c.ClaTotalBalanceCC <= maxTotalBalance.Value);
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
                        c.ClaTotalChargeTRIG == searchDecimal ||
                        (c.ClaTotalAmtPaidCC.HasValue && c.ClaTotalAmtPaidCC.Value == searchDecimal) ||
                        (c.ClaTotalBalanceCC.HasValue && c.ClaTotalBalanceCC.Value == searchDecimal));
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
                                       ClaTotalChargeTRIG = c.ClaTotalChargeTRIG,
                                       ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                                       ClaTotalBalanceCC = c.ClaTotalBalanceCC,
                                       ClaClassification = c.ClaClassification,
                                       ClaPatFID = c.ClaPatFID,
                                       ClaAttendingPhyFID = c.ClaAttendingPhyFID,
                                       ClaBillingPhyFID = c.ClaBillingPhyFID,
                                       ClaReferringPhyFID = c.ClaReferringPhyFID,
                                       ClaBillDate = c.ClaBillDate,
                                       ClaTypeOfBill = c.ClaTypeOfBill,
                                       ClaAdmissionType = c.ClaAdmissionType,
                                       ClaPatientStatus = c.ClaPatientStatus,
                                       ClaDiagnosis1 = c.ClaDiagnosis1,
                                       ClaDiagnosis2 = c.ClaDiagnosis2,
                                       ClaDiagnosis3 = c.ClaDiagnosis3,
                                       ClaDiagnosis4 = c.ClaDiagnosis4,
                                       ClaFirstDateTRIG = c.ClaFirstDateTRIG,
                                       ClaLastDateTRIG = c.ClaLastDateTRIG
                                   },
                                   PatFirstName = hasPatFirstName ? (p != null ? p.PatFirstName : null) : null,
                                   PatLastName = hasPatLastName ? (p != null ? p.PatLastName : null) : null,
                                   PatFullNameCC = hasPatFullNameCC ? (p != null ? p.PatFullNameCC : null) : null,
                                   PatAccountNo = hasPatAccountNo ? (p != null ? p.PatAccountNo : null) : null,
                                   PatPhoneNo = hasPatPhoneNo ? (p != null ? p.PatPhoneNo : null) : null,
                                   PatCity = hasPatCity ? (p != null ? p.PatCity : null) : null,
                                   PatState = hasPatState ? (p != null ? p.PatState : null) : null,
                                   PatBirthDate = hasPatBirthDate ? (p != null ? p.PatBirthDate : (DateOnly?)null) : null,
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
                            "patAccountNo" => d.PatAccountNo,
                            "patPhoneNo" => d.PatPhoneNo,
                            "patCity" => d.PatCity,
                            "patState" => d.PatState,
                            "patBirthDate" => d.PatBirthDate,
                            "renderingPhyName" => d.RenderingPhyName,
                            "renderingPhyNPI" => d.RenderingPhyNPI,
                            "billingPhyName" => d.BillingPhyName,
                            "billingPhyNPI" => d.BillingPhyNPI,
                            "facilityName" => d.FacilityPhyName,
                            _ => null
                        };
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
                        ClaTotalChargeTRIG = c.ClaTotalChargeTRIG,
                        ClaTotalAmtPaidCC = c.ClaTotalAmtPaidCC,
                        ClaTotalBalanceCC = c.ClaTotalBalanceCC,
                        ClaClassification = c.ClaClassification,
                        ClaPatFID = c.ClaPatFID,
                        ClaAttendingPhyFID = c.ClaAttendingPhyFID,
                        ClaBillingPhyFID = c.ClaBillingPhyFID,
                        ClaReferringPhyFID = c.ClaReferringPhyFID,
                        ClaBillDate = c.ClaBillDate,
                        ClaTypeOfBill = c.ClaTypeOfBill,
                        ClaAdmissionType = c.ClaAdmissionType,
                        ClaPatientStatus = c.ClaPatientStatus,
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

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Claim"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
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
                var content = await _claimExportService.Generate837Async(claId);
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
                // Use timeout protection (20 seconds - reduced since we removed adjustments)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                // Step 1: Get basic claim header data (NO navigation properties)
                var claimBase = await _db.Claims
                    .AsNoTracking()
                    .Where(c => c.ClaID == claId)
                    .Select(c => new
                    {
                        c.ClaID,
                        c.ClaPatFID,
                        c.ClaStatus,
                        c.ClaDateTimeCreated,
                        c.ClaDateTimeModified,
                        c.ClaTotalChargeTRIG,
                        c.ClaTotalAmtPaidCC,
                        c.ClaTotalBalanceCC,
                        c.ClaTotalAmtAppliedCC,
                        c.ClaBillDate,
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
                        c.ClaAdmittedDate,
                        c.ClaDischargedDate,
                        c.ClaDateLastSeen,
                        c.ClaRelatedTo,
                        c.ClaRelatedToState,
                        c.ClaFirstDateTRIG,
                        c.ClaLastDateTRIG,
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
                        c.ClaRenderingPhyFID,
                        c.ClaReferringPhyFID,
                        c.ClaBillingPhyFID,
                        c.ClaFacilityPhyFID
                    })
                    .FirstOrDefaultAsync(cts.Token);

                if (claimBase == null)
                {
                    return NotFound();
                }

                // Step 2: Load patient separately using ClaPatFID
                var patient = claimBase.ClaPatFID > 0
                    ? await _db.Patients
                        .AsNoTracking()
                        .Where(p => p.PatID == claimBase.ClaPatFID)
                        .Select(p => new
                        {
                            p.PatID,
                            p.PatFirstName,
                            p.PatLastName,
                            p.PatFullNameCC,
                            p.PatBirthDate,
                            p.PatAccountNo,
                            p.PatPhoneNo,
                            p.PatCity,
                            p.PatState
                        })
                        .FirstOrDefaultAsync(cts.Token)
                    : null;

                // Step 3: Load physicians separately (query individually for null-safety)
                var renderingPhysician = claimBase.ClaRenderingPhyFID > 0
                    ? await _db.Physicians
                        .AsNoTracking()
                        .Where(p => p.PhyID == claimBase.ClaRenderingPhyFID)
                        .Select(p => new { p.PhyID, p.PhyName, p.PhyNPI })
                        .FirstOrDefaultAsync(cts.Token)
                    : null;

                var referringPhysician = claimBase.ClaReferringPhyFID > 0
                    ? await _db.Physicians
                        .AsNoTracking()
                        .Where(p => p.PhyID == claimBase.ClaReferringPhyFID)
                        .Select(p => new { p.PhyID, p.PhyName, p.PhyNPI })
                        .FirstOrDefaultAsync(cts.Token)
                    : null;

                var billingPhysician = claimBase.ClaBillingPhyFID > 0
                    ? await _db.Physicians
                        .AsNoTracking()
                        .Where(p => p.PhyID == claimBase.ClaBillingPhyFID)
                        .Select(p => new { p.PhyID, p.PhyName, p.PhyNPI })
                        .FirstOrDefaultAsync(cts.Token)
                    : null;

                var facilityPhysician = claimBase.ClaFacilityPhyFID > 0
                    ? await _db.Physicians
                        .AsNoTracking()
                        .Where(p => p.PhyID == claimBase.ClaFacilityPhyFID)
                        .Select(p => new { p.PhyID, p.PhyName, p.PhyNPI })
                        .FirstOrDefaultAsync(cts.Token)
                    : null;

                // Step 4: Get service lines and responsible parties in parallel for better performance
                var serviceLinesTask = _db.Service_Lines
                    .AsNoTracking()
                    .Where(s => s.SrvClaFID == claId)
                    .Select(s => new
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
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        s.SrvTotalAdjCC,
                        s.SrvTotalAmtAppliedCC,
                        s.SrvResponsibleParty
                    })
                    .ToListAsync(cts.Token);

                // Execute service lines query first to get responsible party IDs
                var serviceLines = await serviceLinesTask;

                // Step 5: Get responsible party names (only if needed)
                var responsiblePartyIds = serviceLines
                    .Where(s => s.SrvResponsibleParty > 0)
                    .Select(s => s.SrvResponsibleParty)
                    .Distinct()
                    .ToList();
                
                var responsiblePartyDict = new Dictionary<int, string?>();
                if (responsiblePartyIds.Any())
                {
                    var responsibleParties = await _db.Payers
                        .AsNoTracking()
                        .Where(p => responsiblePartyIds.Contains(p.PayID))
                        .Select(p => new { p.PayID, p.PayName })
                        .ToListAsync(cts.Token);
                    
                    responsiblePartyDict = responsibleParties.ToDictionary(p => p.PayID, p => p.PayName);
                }

                // Load Claim_Audit activity (claim-specific only, NOT interface import logs)
                var claimActivityList = new List<object>();
                try
                {
                    var audits = await _db.Claim_Audits
                        .AsNoTracking()
                        .Where(a => a.ClaFID == claId)
                        .OrderByDescending(a => a.ActivityDate)
                        .Select(a => new { date = a.ActivityDate, user = a.UserName ?? "SYSTEM", activityType = a.ActivityType, notes = a.Notes, totalCharge = a.TotalCharge, insuranceBalance = a.InsuranceBalance, patientBalance = a.PatientBalance })
                        .ToListAsync(cts.Token);
                    foreach (var a in audits)
                        claimActivityList.Add(new { date = a.date, user = a.user, activityType = a.activityType, notes = a.notes, totalCharge = a.totalCharge, insuranceBalance = a.insuranceBalance, patientBalance = a.patientBalance });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Claim_Audit table may not exist. Skipping claim activity for claim {ClaId}.", claId);
                }

                // Step 6: Build the response object
                // NOTE: Adjustments are NOT loaded here - they are loaded separately via /api/adjustments/claims/{claId}
                // This significantly improves performance and prevents timeouts
                // Convert DateOnly to DateTime for JSON serialization compatibility
                var claim = new
                {
                    claimBase.ClaID,
                    ClaPatFID = claimBase.ClaPatFID,
                    claimBase.ClaStatus,
                    ClaDateTimeCreated = claimBase.ClaDateTimeCreated,
                    ClaDateTimeModified = claimBase.ClaDateTimeModified,
                    claimBase.ClaTotalChargeTRIG,
                    claimBase.ClaTotalAmtPaidCC,
                    claimBase.ClaTotalBalanceCC,
                    claimBase.ClaTotalAmtAppliedCC,
                    ClaBillDate = claimBase.ClaBillDate.HasValue 
                        ? claimBase.ClaBillDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    claimBase.ClaBillTo,
                    claimBase.ClaSubmissionMethod,
                    claimBase.ClaInvoiceNumber,
                    claimBase.ClaLocked,
                    claimBase.ClaOriginalRefNo,
                    claimBase.ClaDelayCode,
                    claimBase.ClaMedicaidResubmissionCode,
                    claimBase.ClaPaperWorkTransmissionCode,
                    claimBase.ClaPaperWorkControlNumber,
                    claimBase.ClaPaperWorkInd,
                    claimBase.ClaEDINotes,
                    claimBase.ClaRemarks,
                    ClaAdmittedDate = claimBase.ClaAdmittedDate.HasValue 
                        ? claimBase.ClaAdmittedDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    ClaDischargedDate = claimBase.ClaDischargedDate.HasValue 
                        ? claimBase.ClaDischargedDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    ClaDateLastSeen = claimBase.ClaDateLastSeen.HasValue 
                        ? claimBase.ClaDateLastSeen.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    claimBase.ClaRelatedTo,
                    claimBase.ClaRelatedToState,
                    ClaFirstDateTRIG = claimBase.ClaFirstDateTRIG.HasValue 
                        ? claimBase.ClaFirstDateTRIG.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    ClaLastDateTRIG = claimBase.ClaLastDateTRIG.HasValue 
                        ? claimBase.ClaLastDateTRIG.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    claimBase.ClaClassification,
                    claimBase.ClaDiagnosis1,
                    claimBase.ClaDiagnosis2,
                    claimBase.ClaDiagnosis3,
                    claimBase.ClaDiagnosis4,
                    claimBase.ClaDiagnosis5,
                    claimBase.ClaDiagnosis6,
                    claimBase.ClaDiagnosis7,
                    claimBase.ClaDiagnosis8,
                    claimBase.ClaDiagnosis9,
                    claimBase.ClaDiagnosis10,
                    claimBase.ClaDiagnosis11,
                    claimBase.ClaDiagnosis12,
                    Patient = patient != null ? new
                    {
                        patient.PatID,
                        patient.PatFirstName,
                        patient.PatLastName,
                        patient.PatFullNameCC,
                        PatBirthDate = patient.PatBirthDate.HasValue 
                            ? patient.PatBirthDate.Value.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        patient.PatAccountNo,
                        patient.PatPhoneNo,
                        patient.PatCity,
                        patient.PatState
                    } : null,
                    RenderingPhysician = renderingPhysician != null ? new
                    {
                        renderingPhysician.PhyID,
                        renderingPhysician.PhyName,
                        renderingPhysician.PhyNPI
                    } : null,
                    ReferringPhysician = referringPhysician != null ? new
                    {
                        referringPhysician.PhyID,
                        referringPhysician.PhyName,
                        referringPhysician.PhyNPI
                    } : null,
                    BillingPhysician = billingPhysician != null ? new
                    {
                        billingPhysician.PhyID,
                        billingPhysician.PhyName,
                        billingPhysician.PhyNPI
                    } : null,
                    FacilityPhysician = facilityPhysician != null ? new
                    {
                        facilityPhysician.PhyID,
                        facilityPhysician.PhyName,
                        facilityPhysician.PhyNPI
                    } : null,
                    // Service Lines - adjustments and payments loaded separately by frontend
                    ServiceLines = serviceLines.Select(s => new
                    {
                        s.SrvID,
                        SrvFromDate = s.SrvFromDate != default(DateOnly) 
                            ? s.SrvFromDate.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        SrvToDate = s.SrvToDate != default(DateOnly) 
                            ? s.SrvToDate.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        s.SrvProcedureCode,
                        s.SrvDesc,
                        s.SrvCharges,
                        s.SrvUnits,
                        s.SrvPlace,
                        s.SrvDiagnosisPointer,
                        s.SrvTotalBalanceCC,
                        s.SrvTotalAmtPaidCC,
                        s.SrvTotalAdjCC,
                        s.SrvTotalAmtAppliedCC,
                        s.SrvResponsibleParty,
                        ResponsiblePartyName = responsiblePartyDict.ContainsKey(s.SrvResponsibleParty) 
                            ? responsiblePartyDict[s.SrvResponsibleParty] 
                            : (s.SrvResponsibleParty == 0 ? "Patient" : null),
                        // Empty arrays - loaded separately via API endpoints
                        Adjustments = Array.Empty<object>(),
                        Payments = Array.Empty<object>()
                    }).ToList(),
                    ClaimActivity = claimActivityList,
                    AdditionalData = DeserializeClaimAdditionalData(null)
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

            try
            {
                var claim = await _db.Claims.FindAsync(claId);
                if (claim == null)
                    return NotFound();

                claim.ClaClassification = request.ClaClassification != null
                    ? (request.ClaClassification.Length > 30 ? request.ClaClassification[..30] : request.ClaClassification)
                    : null;
                claim.ClaStatus = request.ClaStatus;
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

                await _db.SaveChangesAsync();

                // Insert Claim_Audit record (Claim Edited or manual note) - EZClaim-style history
                try
                {
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
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Claim_Audit insert failed for claim {ClaId}. Claim was updated successfully.", claId);
                }

                _logger.LogInformation("Updated claim {ClaId}, ClaClassification={ClaClassification}", claId, claim.ClaClassification);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating claim {ClaId}", claId);
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


