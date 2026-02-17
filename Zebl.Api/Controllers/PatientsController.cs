using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Patients;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/patients")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PatientsController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(ZeblDbContext db, ICurrentUserContext userContext, ILogger<PatientsController> logger)
        {
            _db = db;
            _userContext = userContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPatients(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? searchText = null,
            [FromQuery] bool? active = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? minPatientId = null,
            [FromQuery] int? maxPatientId = null,
            [FromQuery] int? claimId = null,
            [FromQuery] string? classificationList = null,
            [FromQuery] string? additionalColumns = null)
        {
            try
            {
                if (page < 1 || pageSize < 1 || pageSize > 100)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = "Page must be at least 1 and page size must be between 1 and 100"
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
                var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Patient"];
                var columnsToInclude = availableColumns.Where(c => requestedColumns.Contains(c.Key)).ToList();

                var query = _db.Patients.AsNoTracking();

                // Active filter
                if (active.HasValue)
                {
                    query = query.Where(p => p.PatActive == active.Value);
                }

                // Date range filter
                if (fromDate.HasValue)
                {
                    query = query.Where(p => p.PatDateTimeCreated >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(p => p.PatDateTimeCreated <= toDate.Value);
                }

                // Patient ID range filter
                if (minPatientId.HasValue)
                {
                    query = query.Where(p => p.PatID >= minPatientId.Value);
                }

                if (maxPatientId.HasValue)
                {
                    query = query.Where(p => p.PatID <= maxPatientId.Value);
                }

                // Claim ID filter - filter patients that have a claim with this ID
                if (claimId.HasValue)
                {
                    query = query.Where(p => p.Claims.Any(c => c.ClaID == claimId.Value));
                }

                // Facility (PatClassification) filter - values from Libraries → List → Patient Classification
                if (!string.IsNullOrWhiteSpace(classificationList))
                {
                    var parts = classificationList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    var classifications = parts.Where(s => s != "(Blank)").ToList();
                    var includeBlank = parts.Contains("(Blank)");
                    if (classifications.Count > 0 || includeBlank)
                    {
                        if (classifications.Count > 0 && includeBlank)
                            query = query.Where(p => (p.PatClassification != null && classifications.Contains(p.PatClassification)) || string.IsNullOrEmpty(p.PatClassification));
                        else if (includeBlank)
                            query = query.Where(p => string.IsNullOrEmpty(p.PatClassification));
                        else
                            query = query.Where(p => p.PatClassification != null && classifications.Contains(p.PatClassification));
                    }
                }

                // Text search
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLower().Trim();
                    if (int.TryParse(searchText, out int searchInt))
                    {
                        // If it's a number, check both Patient ID and Claim ID
                        query = query.Where(p => p.PatID == searchInt || p.Claims.Any(c => c.ClaID == searchInt));
                    }
                    else
                    {
                        query = query.Where(p =>
                            (p.PatFirstName != null && p.PatFirstName.ToLower().Contains(searchLower)) ||
                            (p.PatLastName != null && p.PatLastName.ToLower().Contains(searchLower)) ||
                            (p.PatFullNameCC != null && p.PatFullNameCC.ToLower().Contains(searchLower)) ||
                            (p.PatAccountNo != null && p.PatAccountNo.ToLower().Contains(searchLower)) ||
                            (p.PatPhoneNo != null && p.PatPhoneNo.Contains(searchText)));
                    }
                }

                // Order by ID (primary key, should be indexed)
                query = query.OrderByDescending(p => p.PatID);

                // Smart count strategy
                int totalCount;
                bool hasFilters = active.HasValue || fromDate.HasValue || toDate.HasValue ||
                                 minPatientId.HasValue || maxPatientId.HasValue || claimId.HasValue ||
                                 !string.IsNullOrWhiteSpace(classificationList) || !string.IsNullOrWhiteSpace(searchText);

                if (!hasFilters)
                {
                    try
                    {
                        totalCount = await GetApproxPatientCountAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Approximate count failed, using large estimate");
                        totalCount = 1000000;
                    }
                }
                else
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        totalCount = await query.CountAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Patient count query timed out, using approximate count");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                    catch (SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
                    {
                        _logger.LogWarning(sqlEx, "Patient count query timed out (SQL timeout)");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting patient count");
                        try
                        {
                            totalCount = await GetApproxPatientCountAsync();
                        }
                        catch
                        {
                            totalCount = pageSize * (page + 10);
                        }
                    }
                }

                List<PatientListItemDto> data;
                try
                {
                    // Add timeout to prevent hanging queries (20 seconds for data query)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var patientData = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(p => new PatientListItemDto
                        {
                            PatID = p.PatID,
                            PatFirstName = p.PatFirstName,
                            PatLastName = p.PatLastName,
                            PatFullNameCC = p.PatFullNameCC,
                            PatDateTimeCreated = p.PatDateTimeCreated,
                            PatActive = p.PatActive,
                            PatAccountNo = p.PatAccountNo,
                            PatBirthDate = p.PatBirthDate,
                            PatSSN = p.PatSSN,
                            PatSex = p.PatSex,
                            PatAddress = p.PatAddress,
                            PatCity = p.PatCity,
                            PatState = p.PatState,
                            PatZip = p.PatZip,
                            PatPhoneNo = p.PatPhoneNo,
                            PatCellPhoneNo = p.PatCellPhoneNo,
                            PatPriEmail = p.PatPriEmail,
                            PatBillingPhyFID = p.PatBillingPhyFID,
                            PatClassification = p.PatClassification,
                            PatTotalBalanceCC = p.PatTotalBalanceCC,
                            AdditionalColumns = new Dictionary<string, object?>()
                        })
                        .ToListAsync(cts.Token);
                    
                    // For now, Patient has no related columns, but we set up the infrastructure
                    // AdditionalColumns dictionary is initialized but empty
                    data = patientData;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Patient data query timed out (cancellation token)");
                    return StatusCode(503, new ErrorResponseDto
                    {
                        ErrorCode = "QUERY_TIMEOUT",
                        Message = "The query took too long to execute. Please try with filters to narrow down the results."
                    });
                }
                catch (SqlException sqlEx) when (sqlEx.Number == -2 || sqlEx.Number == 2)
                {
                    // SQL Server timeout errors: -2 = Timeout expired, 2 = Timeout during login
                    _logger.LogWarning(sqlEx, "Patient data query timed out (SQL timeout)");
                    return StatusCode(503, new ErrorResponseDto
                    {
                        ErrorCode = "QUERY_TIMEOUT",
                        Message = "The query took too long to execute. Please try with filters to narrow down the results."
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading patient data. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                    return StatusCode(500, new ErrorResponseDto
                    {
                        ErrorCode = "INTERNAL_ERROR",
                        Message = $"An error occurred while loading patient data: {ex.Message}. Please try again or contact support."
                    });
                }

                return Ok(new ApiResponse<List<PatientListItemDto>>
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetPatients. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "INTERNAL_ERROR",
                    Message = $"An unexpected error occurred: {ex.Message}. Please try again or contact support."
                });
            }
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Patient"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        /// <summary>
        /// GET patient by ID with insurance, physicians, and notes from Claim_Audit (all claims belonging to patient).
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPatientById(int id)
        {
            try
            {
                var patient = await _db.Patients.AsNoTracking()
                    .Include(p => p.Patient_Insureds)
                        .ThenInclude(pi => pi.PatInsIns)
                            .ThenInclude(i => i.InsPay)
                    .FirstOrDefaultAsync(p => p.PatID == id);

                if (patient == null)
                    return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = $"Patient {id} not found." });

                // Load physicians (active only)
                var physIds = new[] {
                    patient.PatRenderingPhyFID, patient.PatBillingPhyFID, patient.PatFacilityPhyFID,
                    patient.PatReferringPhyFID, patient.PatOrderingPhyFID, patient.PatSupervisingPhyFID
                }.Where(x => x > 0).Distinct().ToList();

                List<(int PhyID, string? PhyName, string? PhyEntityType, string PhyFullNameCC)> physicians;
                if (physIds.Count > 0)
                {
                    var physData = await _db.Physicians.AsNoTracking()
                        .Where(phy => physIds.Contains(phy.PhyID))
                        .Select(phy => new { phy.PhyID, phy.PhyName, phy.PhyEntityType, phy.PhyFullNameCC })
                        .ToListAsync();
                    physicians = physData.Select(p => (p.PhyID, p.PhyName, p.PhyEntityType, p.PhyFullNameCC)).ToList();
                }
                else
                {
                    physicians = new List<(int PhyID, string? PhyName, string? PhyEntityType, string PhyFullNameCC)>();
                }

                var physDict = physicians.ToDictionary(p => p.PhyID, p => new PhysicianAssignmentDto { PhyID = p.PhyID, PhyName = p.PhyFullNameCC ?? p.PhyName, PhyEntityType = p.PhyEntityType });

                var insuredList = patient.Patient_Insureds
                    .Where(pi => pi.PatInsSequence >= 1 && pi.PatInsSequence <= 5)
                    .OrderBy(pi => pi.PatInsSequence)
                    .ToList();
                var primaryIns = insuredList.FirstOrDefault(pi => pi.PatInsSequence == 1);
                var secondaryIns = insuredList.FirstOrDefault(pi => pi.PatInsSequence == 2);

                InsuranceInfoDto? ToInsDto(Patient_Insured? pi)
                {
                    if (pi?.PatInsIns == null) return null;
                    var ins = pi.PatInsIns;
                    var pay = ins.InsPay;
                    return new InsuranceInfoDto
                    {
                        PatInsGUID = pi.PatInsGUID,
                        PatInsSequence = pi.PatInsSequence,
                        PayID = pay?.PayID ?? 0,
                        PayerName = pay?.PayName,
                        InsGroupNumber = ins.InsGroupNumber,
                        InsIDNumber = ins.InsIDNumber,
                        InsFirstName = ins.InsFirstName,
                        InsLastName = ins.InsLastName,
                        InsMI = ins.InsMI,
                        InsPlanName = ins.InsPlanName,
                        PatInsRelationToInsured = pi.PatInsRelationToInsured,
                        InsBirthDate = ins.InsBirthDate.HasValue ? ins.InsBirthDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        InsAddress = ins.InsAddress,
                        InsCity = ins.InsCity,
                        InsState = ins.InsState,
                        InsZip = ins.InsZip,
                        InsPhone = ins.InsPhone,
                        InsEmployer = ins.InsEmployer,
                        InsAcceptAssignment = ins.InsAcceptAssignment,
                        InsClaimFilingIndicator = ins.InsClaimFilingIndicator,
                        InsSSN = ins.InsSSN,
                        PatInsEligStatus = pi.PatInsEligStatus
                    };
                }

                var patientNotes = new List<PatientNoteDto>();
                try
                {
                    var claimIds = await _db.Claims.AsNoTracking()
                        .Where(c => c.ClaPatFID == id)
                        .Select(c => c.ClaID)
                        .ToListAsync();

                    if (claimIds.Count > 0)
                    {
                        var audits = await _db.Claim_Audits.AsNoTracking()
                            .Where(a => claimIds.Contains(a.ClaFID))
                            .OrderByDescending(a => a.ActivityDate)
                            .Select(a => new { a.ClaFID, a.ActivityDate, a.UserName, a.Notes })
                            .ToListAsync();
                        patientNotes = audits.Select(a => new PatientNoteDto
                        {
                            Date = a.ActivityDate,
                            User = a.UserName ?? "SYSTEM",
                            NoteText = a.Notes,
                            ClaID = a.ClaFID
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Claim_Audit table may not exist. Skipping patient notes for patient {PatId}.", id);
                }

                var dto = new PatientDetailDto
                {
                    PatID = patient.PatID,
                    PatFirstName = patient.PatFirstName,
                    PatLastName = patient.PatLastName,
                    PatMI = patient.PatMI,
                    PatFullNameCC = patient.PatFullNameCC,
                    PatAccountNo = patient.PatAccountNo,
                    PatActive = patient.PatActive,
                    PatBirthDate = patient.PatBirthDate.HasValue ? patient.PatBirthDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    PatSSN = patient.PatSSN,
                    PatSex = patient.PatSex,
                    PatAddress = patient.PatAddress,
                    PatAddress2 = patient.PatAddress2,
                    PatCity = patient.PatCity,
                    PatState = patient.PatState,
                    PatZip = patient.PatZip,
                    PatPhoneNo = patient.PatPhoneNo,
                    PatCellPhoneNo = patient.PatCellPhoneNo,
                    PatHomePhoneNo = patient.PatHomePhoneNo,
                    PatWorkPhoneNo = patient.PatWorkPhoneNo,
                    PatFaxNo = patient.PatFaxNo,
                    PatPriEmail = patient.PatPriEmail,
                    PatSecEmail = patient.PatSecEmail,
                    PatClassification = patient.PatClassification,
                    PatClaLibFID = patient.PatClaLibFID,
                    PatCoPayAmount = patient.PatCoPayAmount,
                    PatDiagnosis1 = patient.PatDiagnosis1,
                    PatDiagnosis2 = patient.PatDiagnosis2,
                    PatDiagnosis3 = patient.PatDiagnosis3,
                    PatDiagnosis4 = patient.PatDiagnosis4,
                    PatEmployed = patient.PatEmployed,
                    PatMarried = patient.PatMarried,
                    PatRenderingPhyFID = patient.PatRenderingPhyFID,
                    PatBillingPhyFID = patient.PatBillingPhyFID,
                    PatFacilityPhyFID = patient.PatFacilityPhyFID,
                    PatReferringPhyFID = patient.PatReferringPhyFID,
                    PatOrderingPhyFID = patient.PatOrderingPhyFID,
                    PatSupervisingPhyFID = patient.PatSupervisingPhyFID,
                    PatStatementName = patient.PatStatementName,
                    PatStatementAddressLine1 = patient.PatStatementAddressLine1,
                    PatStatementAddressLine2 = patient.PatStatementAddressLine2,
                    PatStatementCity = patient.PatStatementCity,
                    PatStatementState = patient.PatStatementState,
                    PatStatementZipCode = patient.PatStatementZipCode,
                    PatStatementMessage = patient.PatStatementMessage,
                    PatReminderNote = patient.PatReminderNote,
                    PatEmergencyContactName = patient.PatEmergencyContactName,
                    PatEmergencyContactPhoneNo = patient.PatEmergencyContactPhoneNo,
                    PatEmergencyContactRelation = patient.PatEmergencyContactRelation,
                    PatWeight = patient.PatWeight,
                    PatHeight = patient.PatHeight,
                    PatMemberID = patient.PatMemberID,
                    PatSigOnFile = patient.PatSigOnFile,
                    PatInsuredSigOnFile = patient.PatInsuredSigOnFile,
                    PatPrintSigDate = patient.PatPrintSigDate,
                    PatPhyPrintDate = patient.PatPhyPrintDate,
                    PatDontSendPromotions = patient.PatDontSendPromotions,
                    PatDontSendStatements = patient.PatDontSendStatements,
                    PatAuthTracking = patient.PatAuthTracking,
                    PatAptReminderPref = patient.PatAptReminderPref,
                    PatReminderNoteEvent = patient.PatReminderNoteEvent,
                    PatSigSource = patient.PatSigSource,
                    PatCoPayPercent = patient.PatCoPayPercent,
                    PatCustomField1 = patient.PatCustomField1,
                    PatCustomField2 = patient.PatCustomField2,
                    PatCustomField3 = patient.PatCustomField3,
                    PatCustomField4 = patient.PatCustomField4,
                    PatCustomField5 = patient.PatCustomField5,
                    PatExternalFID = patient.PatExternalFID,
                    PatPaymentMatchingKey = patient.PatPaymentMatchingKey,
                    PatLastStatementDateTRIG = patient.PatLastStatementDateTRIG,
                    PatTotalBalanceCC = patient.PatTotalBalanceCC,
                    PatDateTimeCreated = patient.PatDateTimeCreated,
                    PatDateTimeModified = patient.PatDateTimeModified,
                    PrimaryInsurance = ToInsDto(primaryIns),
                    SecondaryInsurance = ToInsDto(secondaryIns),
                    InsuranceList = insuredList.Select(ToInsDto).Where(x => x != null).Cast<InsuranceInfoDto>().ToList(),
                    RenderingPhysician = physDict.GetValueOrDefault(patient.PatRenderingPhyFID),
                    BillingPhysician = physDict.GetValueOrDefault(patient.PatBillingPhyFID),
                    FacilityPhysician = physDict.GetValueOrDefault(patient.PatFacilityPhyFID),
                    ReferringPhysician = physDict.GetValueOrDefault(patient.PatReferringPhyFID),
                    OrderingPhysician = physDict.GetValueOrDefault(patient.PatOrderingPhyFID),
                    SupervisingPhysician = physDict.GetValueOrDefault(patient.PatSupervisingPhyFID),
                    PatientNotes = patientNotes
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patient {Id}", id);
                return StatusCode(500, new ErrorResponseDto { ErrorCode = "INTERNAL_ERROR", Message = "Failed to load patient." });
            }
        }

        /// <summary>
        /// PUT update patient. Inserts Claim_Audit for all active claims of this patient when any property changed.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePatient(int id, [FromBody] UpdatePatientRequest request)
        {
            if (request == null)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Request body is required." });

            try
            {
                var patient = await _db.Patients
                    .Include(p => p.Patient_Insureds)
                        .ThenInclude(pi => pi.PatInsIns)
                    .FirstOrDefaultAsync(p => p.PatID == id);

                if (patient == null)
                    return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = $"Patient {id} not found." });

                var userName = _userContext.UserName ?? "SYSTEM";
                var computerName = _userContext.ComputerName ?? Environment.MachineName;
                var noteText = !string.IsNullOrWhiteSpace(request.NoteText)
                    ? (request.NoteText.Trim().Length > 500 ? request.NoteText.Trim()[..500] : request.NoteText.Trim())
                    : "Patient information updated from Patient screen.";

                bool changed = false;

                if (request.PatFirstName != null) { patient.PatFirstName = request.PatFirstName; changed = true; }
                if (request.PatLastName != null) { patient.PatLastName = request.PatLastName; changed = true; }
                if (request.PatMI != null) { patient.PatMI = request.PatMI; changed = true; }
                if (request.PatAccountNo != null) { patient.PatAccountNo = request.PatAccountNo; changed = true; }
                if (request.PatActive.HasValue) { patient.PatActive = request.PatActive.Value; changed = true; }
                if (request.PatBirthDate.HasValue) { patient.PatBirthDate = DateOnly.FromDateTime(request.PatBirthDate.Value); changed = true; }
                if (request.PatSSN != null) { patient.PatSSN = request.PatSSN; changed = true; }
                if (request.PatSex != null) { patient.PatSex = request.PatSex; changed = true; }
                if (request.PatAddress != null) { patient.PatAddress = request.PatAddress; changed = true; }
                if (request.PatAddress2 != null) { patient.PatAddress2 = request.PatAddress2; changed = true; }
                if (request.PatCity != null) { patient.PatCity = request.PatCity; changed = true; }
                if (request.PatState != null) { patient.PatState = request.PatState; changed = true; }
                if (request.PatZip != null) { patient.PatZip = request.PatZip; changed = true; }
                if (request.PatPhoneNo != null) { patient.PatPhoneNo = request.PatPhoneNo; changed = true; }
                if (request.PatCellPhoneNo != null) { patient.PatCellPhoneNo = request.PatCellPhoneNo; changed = true; }
                if (request.PatHomePhoneNo != null) { patient.PatHomePhoneNo = request.PatHomePhoneNo; changed = true; }
                if (request.PatWorkPhoneNo != null) { patient.PatWorkPhoneNo = request.PatWorkPhoneNo; changed = true; }
                if (request.PatFaxNo != null) { patient.PatFaxNo = request.PatFaxNo; changed = true; }
                if (request.PatPriEmail != null) { patient.PatPriEmail = request.PatPriEmail; changed = true; }
                if (request.PatSecEmail != null) { patient.PatSecEmail = request.PatSecEmail; changed = true; }
                if (request.PatClassification != null) { patient.PatClassification = request.PatClassification; changed = true; }
                if (request.PatClaLibFID.HasValue) { patient.PatClaLibFID = request.PatClaLibFID.Value; changed = true; }
                if (request.PatCoPayAmount.HasValue) { patient.PatCoPayAmount = request.PatCoPayAmount.Value; changed = true; }
                if (request.PatDiagnosis1 != null) { patient.PatDiagnosis1 = request.PatDiagnosis1; changed = true; }
                if (request.PatDiagnosis2 != null) { patient.PatDiagnosis2 = request.PatDiagnosis2; changed = true; }
                if (request.PatDiagnosis3 != null) { patient.PatDiagnosis3 = request.PatDiagnosis3; changed = true; }
                if (request.PatDiagnosis4 != null) { patient.PatDiagnosis4 = request.PatDiagnosis4; changed = true; }
                if (request.PatEmployed.HasValue) { patient.PatEmployed = request.PatEmployed.Value; changed = true; }
                if (request.PatMarried.HasValue) { patient.PatMarried = request.PatMarried.Value; changed = true; }
                if (request.PatRenderingPhyFID.HasValue) { patient.PatRenderingPhyFID = request.PatRenderingPhyFID.Value; changed = true; }
                if (request.PatBillingPhyFID.HasValue) { patient.PatBillingPhyFID = request.PatBillingPhyFID.Value; changed = true; }
                if (request.PatFacilityPhyFID.HasValue) { patient.PatFacilityPhyFID = request.PatFacilityPhyFID.Value; changed = true; }
                if (request.PatReferringPhyFID.HasValue) { patient.PatReferringPhyFID = request.PatReferringPhyFID.Value; changed = true; }
                if (request.PatOrderingPhyFID.HasValue) { patient.PatOrderingPhyFID = request.PatOrderingPhyFID.Value; changed = true; }
                if (request.PatSupervisingPhyFID.HasValue) { patient.PatSupervisingPhyFID = request.PatSupervisingPhyFID.Value; changed = true; }
                if (request.PatStatementName != null) { patient.PatStatementName = request.PatStatementName; changed = true; }
                if (request.PatStatementAddressLine1 != null) { patient.PatStatementAddressLine1 = request.PatStatementAddressLine1; changed = true; }
                if (request.PatStatementAddressLine2 != null) { patient.PatStatementAddressLine2 = request.PatStatementAddressLine2; changed = true; }
                if (request.PatStatementCity != null) { patient.PatStatementCity = request.PatStatementCity; changed = true; }
                if (request.PatStatementState != null) { patient.PatStatementState = request.PatStatementState; changed = true; }
                if (request.PatStatementZipCode != null) { patient.PatStatementZipCode = request.PatStatementZipCode; changed = true; }
                if (request.PatStatementMessage != null) { patient.PatStatementMessage = request.PatStatementMessage; changed = true; }
                if (request.PatReminderNote != null) { patient.PatReminderNote = request.PatReminderNote; changed = true; }
                if (request.PatEmergencyContactName != null) { patient.PatEmergencyContactName = request.PatEmergencyContactName; changed = true; }
                if (request.PatEmergencyContactPhoneNo != null) { patient.PatEmergencyContactPhoneNo = request.PatEmergencyContactPhoneNo; changed = true; }
                if (request.PatEmergencyContactRelation != null) { patient.PatEmergencyContactRelation = request.PatEmergencyContactRelation; changed = true; }
                if (request.PatWeight != null) { patient.PatWeight = request.PatWeight; changed = true; }
                if (request.PatHeight != null) { patient.PatHeight = request.PatHeight; changed = true; }
                if (request.PatMemberID != null) { patient.PatMemberID = request.PatMemberID; changed = true; }
                if (request.PatSigOnFile.HasValue) { patient.PatSigOnFile = request.PatSigOnFile.Value; changed = true; }
                if (request.PatInsuredSigOnFile.HasValue) { patient.PatInsuredSigOnFile = request.PatInsuredSigOnFile.Value; changed = true; }
                if (request.PatPrintSigDate.HasValue) { patient.PatPrintSigDate = request.PatPrintSigDate.Value; changed = true; }
                if (request.PatPhyPrintDate.HasValue) { patient.PatPhyPrintDate = request.PatPhyPrintDate.Value; changed = true; }
                if (request.PatDontSendPromotions.HasValue) { patient.PatDontSendPromotions = request.PatDontSendPromotions.Value; changed = true; }
                if (request.PatDontSendStatements.HasValue) { patient.PatDontSendStatements = request.PatDontSendStatements.Value; changed = true; }
                if (request.PatAuthTracking.HasValue) { patient.PatAuthTracking = request.PatAuthTracking.Value; changed = true; }
                if (request.PatAptReminderPref != null) { patient.PatAptReminderPref = request.PatAptReminderPref; changed = true; }
                if (request.PatReminderNoteEvent != null) { patient.PatReminderNoteEvent = request.PatReminderNoteEvent; changed = true; }
                if (request.PatSigSource != null) { patient.PatSigSource = request.PatSigSource; changed = true; }
                if (request.PatCoPayPercent.HasValue) { patient.PatCoPayPercent = request.PatCoPayPercent.Value; changed = true; }
                if (request.PatCustomField1 != null) { patient.PatCustomField1 = request.PatCustomField1; changed = true; }
                if (request.PatCustomField2 != null) { patient.PatCustomField2 = request.PatCustomField2; changed = true; }
                if (request.PatCustomField3 != null) { patient.PatCustomField3 = request.PatCustomField3; changed = true; }
                if (request.PatCustomField4 != null) { patient.PatCustomField4 = request.PatCustomField4; changed = true; }
                if (request.PatCustomField5 != null) { patient.PatCustomField5 = request.PatCustomField5; changed = true; }
                if (request.PatExternalFID != null) { patient.PatExternalFID = request.PatExternalFID; changed = true; }
                if (request.PatPaymentMatchingKey != null) { patient.PatPaymentMatchingKey = request.PatPaymentMatchingKey; changed = true; }
                if (request.PatLastStatementDateTRIG.HasValue) { patient.PatLastStatementDateTRIG = request.PatLastStatementDateTRIG.Value; changed = true; }

                bool insuranceModified = false;
                if (request.InsuranceList != null)
                {
                    changed = true;
                    insuranceModified = await ApplyInsuranceUpdates(patient, request.InsuranceList);
                }

                await _db.SaveChangesAsync();

                if (changed)
                {
                    try
                    {
                        var auditNote = insuranceModified ? "Insurance sequence modified." : noteText;
                        var activityType = insuranceModified ? "Insurance Updated" : "Patient Updated";

                        var claimIds = await _db.Claims.AsNoTracking()
                            .Where(c => c.ClaPatFID == id && (c.ClaArchived == null || c.ClaArchived == false))
                            .Select(c => c.ClaID)
                            .ToListAsync();

                        foreach (var claId in claimIds)
                        {
                            var claim = await _db.Claims.AsNoTracking()
                                .Where(c => c.ClaID == claId)
                                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                                .FirstOrDefaultAsync();
                            var snapshot = claim;
                            _db.Claim_Audits.Add(new Claim_Audit
                            {
                                ClaFID = claId,
                                ActivityType = activityType,
                                ActivityDate = DateTime.UtcNow,
                                UserName = userName,
                                ComputerName = computerName,
                                Notes = auditNote,
                                TotalCharge = snapshot?.ClaTotalChargeTRIG,
                                InsuranceBalance = snapshot?.ClaTotalInsBalanceTRIG,
                                PatientBalance = snapshot?.ClaTotalPatBalanceTRIG
                            });
                        }
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Claim_Audit insert failed for patient {PatId}. Patient was updated successfully.", id);
                    }
                }

                _logger.LogInformation("Updated patient {PatId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient {Id}", id);
                return StatusCode(500, new ErrorResponseDto { ErrorCode = "INTERNAL_ERROR", Message = "Failed to update patient." });
            }
        }

        private async Task<bool> ApplyInsuranceUpdates(Patient patient, List<InsuranceUpdateDto> insuranceList)
        {
            if (insuranceList == null || insuranceList.Count == 0)
                return false;

            var primaryCount = insuranceList.Count(x => x.Sequence == 1);
            if (primaryCount > 1)
                throw new InvalidOperationException("Only one insurance record can have sequence=1 (primary).");

            var requestedGuids = insuranceList.Where(x => x.PatInsGUID.HasValue).Select(x => x.PatInsGUID!.Value).ToHashSet();
            var toRemove = patient.Patient_Insureds.Where(pi => !requestedGuids.Contains(pi.PatInsGUID)).ToList();
            foreach (var pi in toRemove)
                _db.Patient_Insureds.Remove(pi);

            bool modified = false;
            foreach (var ins in insuranceList.OrderBy(x => x.Sequence))
            {
                if (ins.PatInsGUID.HasValue)
                {
                    var existing = patient.Patient_Insureds.FirstOrDefault(pi => pi.PatInsGUID == ins.PatInsGUID.Value);
                    if (existing != null)
                    {
                        if (existing.PatInsSequence != ins.Sequence) modified = true;
                        existing.PatInsSequence = ins.Sequence;
                        existing.PatInsSequenceDescriptionCC = ins.Sequence == 1 ? "Primary" : ins.Sequence == 2 ? "Secondary" : $"Insurance {ins.Sequence}";
                        if (existing.PatInsIns != null)
                        {
                            if (ins.PayID.HasValue) { existing.PatInsIns.InsPayID = ins.PayID.Value; modified = true; }
                            if (ins.GroupNumber != null) { existing.PatInsIns.InsGroupNumber = ins.GroupNumber.Length > 50 ? ins.GroupNumber[..50] : ins.GroupNumber; modified = true; }
                            if (ins.MemberID != null) { existing.PatInsIns.InsIDNumber = ins.MemberID.Length > 50 ? ins.MemberID[..50] : ins.MemberID; modified = true; }
                            if (ins.InsFirstName != null) { existing.PatInsIns.InsFirstName = ins.InsFirstName.Length > 50 ? ins.InsFirstName[..50] : ins.InsFirstName; modified = true; }
                            if (ins.InsLastName != null) { existing.PatInsIns.InsLastName = ins.InsLastName.Length > 50 ? ins.InsLastName[..50] : ins.InsLastName; modified = true; }
                            if (ins.InsMI != null) { existing.PatInsIns.InsMI = ins.InsMI.Length > 5 ? ins.InsMI[..5] : ins.InsMI; modified = true; }
                            if (ins.PlanName != null) { existing.PatInsIns.InsPlanName = ins.PlanName.Length > 50 ? ins.PlanName[..50] : ins.PlanName; modified = true; }
                            existing.PatInsRelationToInsured = ins.RelationToInsured;
                            if (ins.InsBirthDate.HasValue) { existing.PatInsIns.InsBirthDate = DateOnly.FromDateTime(ins.InsBirthDate.Value); modified = true; }
                            if (ins.InsAddress != null) { existing.PatInsIns.InsAddress = ins.InsAddress.Length > 50 ? ins.InsAddress[..50] : ins.InsAddress; modified = true; }
                            if (ins.InsCity != null) { existing.PatInsIns.InsCity = ins.InsCity.Length > 50 ? ins.InsCity[..50] : ins.InsCity; modified = true; }
                            if (ins.InsState != null) { existing.PatInsIns.InsState = ins.InsState.Length > 10 ? ins.InsState[..10] : ins.InsState; modified = true; }
                            if (ins.InsZip != null) { existing.PatInsIns.InsZip = ins.InsZip.Length > 20 ? ins.InsZip[..20] : ins.InsZip; modified = true; }
                            if (ins.InsPhone != null) { existing.PatInsIns.InsPhone = ins.InsPhone.Length > 25 ? ins.InsPhone[..25] : ins.InsPhone; modified = true; }
                            if (ins.InsEmployer != null) { existing.PatInsIns.InsEmployer = ins.InsEmployer.Length > 50 ? ins.InsEmployer[..50] : ins.InsEmployer; modified = true; }
                            if (ins.InsAcceptAssignment.HasValue) { existing.PatInsIns.InsAcceptAssignment = ins.InsAcceptAssignment.Value; modified = true; }
                            if (ins.InsClaimFilingIndicator != null) { existing.PatInsIns.InsClaimFilingIndicator = ins.InsClaimFilingIndicator.Length > 5 ? ins.InsClaimFilingIndicator[..5] : ins.InsClaimFilingIndicator; modified = true; }
                            if (ins.InsSSN != null) { existing.PatInsIns.InsSSN = ins.InsSSN.Length > 15 ? ins.InsSSN[..15] : ins.InsSSN; modified = true; }
                        }
                    }
                }
                else if (ins.PayID.HasValue)
                {
                    var payer = await _db.Payers.FirstOrDefaultAsync(p => p.PayID == ins.PayID.Value);
                    if (payer == null) continue;

                    for (int s = 5; s >= 1; s--)
                    {
                        var atSeq = patient.Patient_Insureds.FirstOrDefault(pi => pi.PatInsSequence == s);
                        if (atSeq != null && s < 5)
                        {
                            atSeq.PatInsSequence = s + 1;
                            atSeq.PatInsSequenceDescriptionCC = s + 1 == 2 ? "Secondary" : $"Insurance {s + 1}";
                            modified = true;
                        }
                        else if (atSeq != null && s == 5)
                        {
                            _db.Patient_Insureds.Remove(atSeq);
                            modified = true;
                        }
                    }

                    var newInsured = new Insured
                    {
                        InsGUID = Guid.NewGuid(),
                        InsPayID = ins.PayID.Value,
                        InsGroupNumber = ins.GroupNumber != null && ins.GroupNumber.Length > 50 ? ins.GroupNumber[..50] : ins.GroupNumber,
                        InsIDNumber = ins.MemberID != null && ins.MemberID.Length > 50 ? ins.MemberID[..50] : ins.MemberID,
                        InsFirstName = ins.InsFirstName != null && ins.InsFirstName.Length > 50 ? ins.InsFirstName[..50] : ins.InsFirstName,
                        InsLastName = ins.InsLastName != null && ins.InsLastName.Length > 50 ? ins.InsLastName[..50] : ins.InsLastName,
                        InsMI = ins.InsMI != null && ins.InsMI.Length > 5 ? ins.InsMI[..5] : ins.InsMI,
                        InsPlanName = ins.PlanName != null && ins.PlanName.Length > 50 ? ins.PlanName[..50] : ins.PlanName,
                        InsCityStateZipCC = "",
                        InsAcceptAssignment = ins.InsAcceptAssignment ?? (short)0,
                        InsBirthDate = ins.InsBirthDate.HasValue ? DateOnly.FromDateTime(ins.InsBirthDate.Value) : null,
                        InsAddress = ins.InsAddress != null && ins.InsAddress.Length > 50 ? ins.InsAddress[..50] : ins.InsAddress,
                        InsCity = ins.InsCity != null && ins.InsCity.Length > 50 ? ins.InsCity[..50] : ins.InsCity,
                        InsState = ins.InsState != null && ins.InsState.Length > 10 ? ins.InsState[..10] : ins.InsState,
                        InsZip = ins.InsZip != null && ins.InsZip.Length > 20 ? ins.InsZip[..20] : ins.InsZip,
                        InsPhone = ins.InsPhone != null && ins.InsPhone.Length > 25 ? ins.InsPhone[..25] : ins.InsPhone,
                        InsEmployer = ins.InsEmployer != null && ins.InsEmployer.Length > 50 ? ins.InsEmployer[..50] : ins.InsEmployer,
                        InsClaimFilingIndicator = ins.InsClaimFilingIndicator != null && ins.InsClaimFilingIndicator.Length > 5 ? ins.InsClaimFilingIndicator[..5] : ins.InsClaimFilingIndicator,
                        InsSSN = ins.InsSSN != null && ins.InsSSN.Length > 15 ? ins.InsSSN[..15] : ins.InsSSN
                    };
                    _db.Insureds.Add(newInsured);
                    await _db.SaveChangesAsync();

                    var newPatIns = new Patient_Insured
                    {
                        PatInsGUID = Guid.NewGuid(),
                        PatInsPatFID = patient.PatID,
                        PatInsInsGUID = newInsured.InsGUID,
                        PatInsSequence = 1,
                        PatInsRelationToInsured = ins.RelationToInsured,
                        PatInsSequenceDescriptionCC = "Primary"
                    };
                    _db.Patient_Insureds.Add(newPatIns);
                    modified = true;
                }
            }
            return modified;
        }

        private async Task<int> GetApproxPatientCountAsync()
        {
            try
            {
                // Fast metadata-based row count (works well for large tables)
                // index_id 0 = heap, 1 = clustered index
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Patient]')
    AND p.index_id IN (0, 1)";

                return await _db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get approximate patient count from metadata, using fallback");
                // Fallback: if sys.* access is restricted, don't break the endpoint
                return 0;
            }
        }
    }
}
