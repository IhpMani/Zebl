using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Service for importing HL7 DFT messages into the database
/// Implements EZClaim-style claim creation: always creates new claims per DFT message
/// </summary>
public class Hl7ImportService
{
    private readonly ZeblDbContext _db;
    private readonly ILogger<Hl7ImportService> _logger;
    private readonly Hl7ParserService _parser;
    private readonly ICurrentUserContext _userContext;
    private readonly IClaimAuditService _claimAuditService;

    public Hl7ImportService(
        ZeblDbContext db,
        ILogger<Hl7ImportService> logger,
        Hl7ParserService parser,
        ICurrentUserContext userContext,
        IClaimAuditService claimAuditService)
    {
        _db = db;
        _logger = logger;
        _parser = parser;
        _userContext = userContext;
        _claimAuditService = claimAuditService;
    }

    /// <summary>
    /// Result of processing HL7 messages with statistics
    /// </summary>
    public class Hl7ImportResult
    {
        public int SuccessCount { get; set; }
        public int NewPatientsCount { get; set; }
        public int UpdatedPatientsCount { get; set; }
        public int NewClaimsCount { get; set; }
        public int DuplicateClaimsCount { get; set; }
        public int NewServiceLinesCount { get; set; }
        public decimal TotalAmount { get; set; }

        // Additional aggregate stats for debugging/monitoring
        public int ClaimsCreated { get; set; }
        public int Errors { get; set; }
    }

    /// <summary>
    /// Processes a list of HL7 DFT messages and creates Patients, Claims, and Service_Lines.
    /// This is the ONLY place where a transaction is managed for HL7 imports.
    /// </summary>
    public async Task<Hl7ImportResult> ProcessHl7Messages(List<Hl7DftMessage> messages, string fileName = "unknown.hl7")
    {
        var result = new Hl7ImportResult();

        _logger.LogInformation("HL7 file import started for {FileName} with {MessageCount} parsed messages", fileName, messages?.Count ?? 0);

        foreach (var message in messages)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.PidSegment))
            {
                _logger.LogWarning("Skipping invalid HL7 message: missing PID segment");
                continue;
            }

            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                var messageStats = await ProcessDftMessage(message, fileName);

                result.SuccessCount++;
                result.NewPatientsCount += messageStats.NewPatient ? 1 : 0;
                result.NewClaimsCount += messageStats.NewClaim ? 1 : 0;
                result.NewServiceLinesCount += messageStats.ServiceLinesCreated;
                result.TotalAmount += messageStats.Amount;

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HL7 message failed but import continues");
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
            }
        }

        return result;
    }

    /// <summary>
    /// Statistics for a single message processing
    /// </summary>
    private class MessageStats
    {
        public bool NewPatient { get; set; }
        public bool NewClaim { get; set; }
        public int ServiceLinesCreated { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Processes a single DFT message: Patient → Claim → Service_Lines
    /// Returns statistics about what was created
    /// </summary>
    private async Task<MessageStats> ProcessDftMessage(Hl7DftMessage message, string fileName)
    {
        var stats = new MessageStats();

        // Step 1: Extract patient MRN from PID-3
        var mrn = _parser.ExtractPatientMrn(message.PidSegment);
        if (string.IsNullOrWhiteSpace(mrn))
        {
            throw new InvalidOperationException("Cannot process DFT message: PID-3 (MRN) is missing");
        }

        // Step 2: Match or create Patient
        var (patient, isNewPatient) = await MatchOrCreatePatient(message.PidSegment!, mrn);
        if (patient == null || patient.PatID <= 0)
        {
            throw new InvalidOperationException($"Failed to create or retrieve patient with MRN: {mrn}");
        }
        stats.NewPatient = isNewPatient;

        // -------------------------------------------------------------------------
        // IMPORT ORDER: Claim MUST be persisted before any Service_Line.
        // We do NOT rely on EF ordering. We explicitly save the claim first.
        // -------------------------------------------------------------------------

        // Step 3: Create Claim, add to context, and call SaveChangesAsync so ClaID is generated.
        var (claim, isNewClaim) = await CreateNewClaim(patient.PatID, message);
        if (claim == null || claim.ClaID <= 0)
        {
            if (message.Ft1Segments != null && message.Ft1Segments.Count > 0)
            {
                throw new InvalidOperationException(
                    "HL7 import aborted: Claim creation failed but message has FT1 segments. " +
                    "Cannot insert Service_Line without a valid Claim. Fix claim/patient/physician data and retry.");
            }
            throw new InvalidOperationException($"Failed to create claim for patient {patient.PatID}");
        }
        stats.NewClaim = isNewClaim;

        // Claim is now in the database; claim.ClaID is set. Only after this point do we create Service_Line records.
        _logger.LogInformation("HL7 Claim persisted with ClaID {ClaimId}; creating service lines.", claim.ClaID);

        var claimFirstDate = claim.ClaFirstDateTRIG ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Step 4: After claim is saved, iterate FT1 segments and add Service_Line entities (no SaveChanges in loop).
        int serviceLinesCreated = 0;
        decimal amount = 0m;
        foreach (var ft1Segment in message.Ft1Segments)
        {
            var (created, charges) = await CreateServiceLineFromFt1WithCharges(claim.ClaID, ft1Segment, claimFirstDate);
            if (created) serviceLinesCreated++;
            else _logger.LogDebug("Skipped FT1 segment for claim {ClaimId}: duplicate or invalid. Segment: {FT1}", claim.ClaID, ft1Segment);
            amount += charges;
        }

        if (serviceLinesCreated == 0)
        {
            _logger.LogWarning("No FT1 service lines detected for claim {ClaID}. Creating default HL7 service line.", claim.ClaID);
            var defaultFt1 = $"FT1|1|||{claimFirstDate:yyyyMMdd}||||||DEFAULT|1";
            var (created, charges) = await CreateServiceLineFromFt1WithCharges(claim.ClaID, defaultFt1, claimFirstDate);
            if (created)
            {
                serviceLinesCreated++;
                amount += charges;
            }
        }

        // Step 5: Persist all Service_Lines in one call (Claim was already saved in Step 3).
        if (serviceLinesCreated > 0)
        {
            await _db.SaveChangesAsync();
        }

        stats.ServiceLinesCreated = serviceLinesCreated;
        stats.Amount = amount;

        // Step 5: Create Claim_Insured records from IN1 segments (primary + secondary only)
        await CreateClaimInsuredRecords(claim.ClaID, patient.PatID, message.In1Segments);

        // Step 5b: Insert Claim_Audit when insurance info was added/updated
        if (message.In1Segments != null && message.In1Segments.Count > 0)
        {
            await _claimAuditService.AddInsuranceEditedAsync(claim.ClaID);
        }

        // Step 6: Update claim totals after all service lines are created
        await UpdateClaimTotals(claim.ClaID);

        // Step 7: Insert Claim_Audit (shows in Claim Note List with filename)
        var noteText = stats.NewClaim
            ? $"Claim Note: Imported from file {fileName}."
            : $"Claim Note: Updated from file {fileName}.";
        var activityType = stats.NewClaim ? "Claim Imported" : "Claim Updated";
        try
        {
            var claimForSnapshot = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claim.ClaID)
                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                .FirstOrDefaultAsync();
            _db.Claim_Audits.Add(new Claim_Audit
            {
                ClaFID = claim.ClaID,
                ActivityType = activityType,
                ActivityDate = DateTime.UtcNow,
                UserName = _userContext.UserName ?? "SYSTEM",
                ComputerName = _userContext.ComputerName ?? Environment.MachineName,
                Notes = noteText,
                TotalCharge = claimForSnapshot?.ClaTotalChargeTRIG,
                InsuranceBalance = claimForSnapshot?.ClaTotalInsBalanceTRIG,
                PatientBalance = claimForSnapshot?.ClaTotalPatBalanceTRIG
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claim_Audit insert failed for claim {ClaId} (DFT import). Import succeeded.", claim.ClaID);
        }

        return stats;
    }

    /// <summary>
    /// Matches patient by MRN (PID-3) or creates new patient
    /// Uses PatAccountNo as the unique identifier
    /// Returns patient and whether it was newly created
    /// </summary>
    private async Task<(Patient patient, bool isNew)> MatchOrCreatePatient(string pidSegment, string mrn)
    {
        // Normalize MRN (trim, truncate to max length)
        var normalizedMrn = _parser.NormalizeString(mrn, maxLength: 50);
        if (string.IsNullOrWhiteSpace(normalizedMrn))
        {
            throw new InvalidOperationException("Cannot process DFT message: PID-3 (MRN) is missing or invalid after normalization");
        }

        // Match by PatAccountNo (MRN)
        var existingPatient = await _db.Patients
            .FirstOrDefaultAsync(p => p.PatAccountNo == normalizedMrn);

        if (existingPatient != null)
        {
            _logger.LogInformation("Found existing patient with MRN {MRN}, PatID: {PatID}", mrn, existingPatient.PatID);
            return (existingPatient, false);
        }

        // Extract patient data from PID segment
        // PID-5: Patient Name (LastName^FirstName^MiddleName)
        var (firstName, lastName) = _parser.ExtractPatientName(pidSegment);
        // Normalize names (truncate to max length)
        firstName = _parser.NormalizeString(firstName, maxLength: 50);
        lastName = _parser.NormalizeString(lastName, maxLength: 50);
        
        // PID-7: Date/Time of Birth
        var birthDate = _parser.ExtractPatientBirthDate(pidSegment);
        
        // PID-11: Patient Address (XAD format: Street^Other^City^State^Zip^Country^...)
        var addressField = _parser.GetFieldValue(pidSegment, 11);
        var (streetAddress, city, state, zip) = _parser.ParseAddress(addressField);
        
        // PID-13: Phone Number (XTN format: [NNN] [(999)]999-9999[X99999]^PRN^PH^^^)
        var phoneField = _parser.GetFieldValue(pidSegment, 13);
        var phone = _parser.SanitizePhoneNumber(phoneField, maxLength: 25);

        // Choose a default physician to satisfy FK constraints for required physician FKs
        var defaultPhysician = await _db.Physicians
            .OrderBy(p => p.PhyID)
            .FirstOrDefaultAsync();

        if (defaultPhysician == null)
        {
            throw new InvalidOperationException(
                "HL7 import requires at least one Physician record. Insert a Physician before importing HL7 messages.");
        }

        var defaultPhysicianId = defaultPhysician.PhyID;
        _logger.LogInformation(
            "Using physician {PhyID} as default for HL7 patient import",
            defaultPhysicianId);

        // Create new patient with all NOT NULL columns populated (EZClaim defaults)
        var newPatient = new Patient
        {
            PatAccountNo = normalizedMrn, // Use normalized MRN
            PatFirstName = firstName,
            PatLastName = lastName,
            PatBirthDate = birthDate,
            PatAddress = streetAddress,
            PatCity = city,
            PatState = state,
            PatZip = zip,
            PatHomePhoneNo = phone,
            // Required boolean fields (NOT NULL) - EZClaim defaults
            PatActive = true,
            PatDontSendPromotions = false,
            PatDontSendStatements = false,
            PatInsuredSigOnFile = false,
            PatLocked = false,
            PatSigOnFile = false,
            // Required string fields (NOT NULL) - EZClaim defaults
            PatAptReminderPref = "None",
            PatReminderNoteEvent = "None",
            PatDiagnosisCodesCC = string.Empty,
            PatFullNameFMLCC = string.Empty,
            PatCityStateZipCC = string.Empty,
            PatStatementCityStateZipCC = string.Empty,
            PatFullNameCC = !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName)
                ? $"{firstName} {lastName}".Trim()
                : normalizedMrn,
            // Required decimal fields (NOT NULL) - EZClaim defaults
            PatTotalInsBalanceTRIG = 0m,
            PatTotalPatBalanceTRIG = 0m,
            PatTotalUndisbursedPaymentsTRIG = 0m,
            // Required foreign keys (NOT NULL) - use default physician to satisfy FK constraints
            PatBillingPhyFID = defaultPhysicianId,
            PatFacilityPhyFID = defaultPhysicianId,
            PatOrderingPhyFID = defaultPhysicianId,
            PatReferringPhyFID = defaultPhysicianId,
            PatRenderingPhyFID = defaultPhysicianId,
            PatSupervisingPhyFID = defaultPhysicianId,
            PatClaLibFID = 0
        };

        await _db.Patients.AddAsync(newPatient);
        await _db.SaveChangesAsync(); // Save to get PatID

        if (newPatient.PatID <= 0)
        {
            throw new InvalidOperationException($"Failed to create patient: PatID is {newPatient.PatID} after SaveChanges");
        }

        _logger.LogInformation("Created new patient with MRN {MRN}, PatID: {PatID}", normalizedMrn, newPatient.PatID);
        return (newPatient, true);
    }

    /// <summary>
    /// Creates a NEW Claim for the DFT message or returns existing if duplicate.
    /// For new claims: adds to DbSet and calls SaveChangesAsync so ClaID is generated before any Service_Line is created.
    /// Do not rely on EF ordering; claim is explicitly persisted here.
    /// </summary>
    private async Task<(Claim claim, bool isNew)> CreateNewClaim(int patientId, Hl7DftMessage message)
    {
        if (patientId <= 0)
        {
            throw new ArgumentException($"Invalid patient ID: {patientId}", nameof(patientId));
        }

        // Extract PV1 data if available
        DateOnly? admittedDate = null;
        DateOnly? dischargedDate = null;
        string? visitNumber = null;
        string? hl7BillingNpi = null;
        string? hl7AttendingNpi = null;

        if (!string.IsNullOrWhiteSpace(message.Pv1Segment))
        {
            // PV1-19: Visit Number (Account/Visit identifier)
            visitNumber = _parser.NormalizeString(_parser.GetFieldValue(message.Pv1Segment, 19), maxLength: 50);

            // PV1-44: Admit Date
            var admitDateStr = _parser.GetFieldValue(message.Pv1Segment, 44);
            admittedDate = _parser.ParseHl7Date(admitDateStr);

            // PV1-45: Discharge Date
            var dischargeDateStr = _parser.GetFieldValue(message.Pv1Segment, 45);
            dischargedDate = _parser.ParseHl7Date(dischargeDateStr);

            // Physician NPIs (if present)
            // PV1-7: Attending doctor, PV1-8: Referring doctor (used as billing when available)
            var attendingField = _parser.GetFieldValue(message.Pv1Segment, 7);
            var referringField = _parser.GetFieldValue(message.Pv1Segment, 8);
            hl7AttendingNpi = _parser.GetComponentValue(attendingField, 0);
            hl7BillingNpi = _parser.GetComponentValue(referringField, 0) ?? hl7AttendingNpi;
        }

        // Get first service date from FT1 segments for claim date range (DOS)
        DateOnly? firstServiceDate = null;
        if (message.Ft1Segments.Count > 0)
        {
            var firstFt1 = message.Ft1Segments[0];
            // FT1-4: Transaction Date (service date) - this is the DOS
            var transactionDateStr = _parser.GetFieldValue(firstFt1, 4);
            firstServiceDate = _parser.ParseHl7Date(transactionDateStr);
        }

        // EZClaim deduplication: Match by Patient + DOS + Visit/Account (AsNoTracking for lookup only)
        if (firstServiceDate.HasValue)
        {
            var existingClaim = await _db.Claims
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.ClaPatFID == patientId &&
                    c.ClaFirstDateTRIG == firstServiceDate.Value &&
                    (visitNumber == null || c.ClaMedicalRecordNumber == visitNumber ||
                     (admittedDate.HasValue && c.ClaAdmittedDate == admittedDate.Value)));

            if (existingClaim != null)
            {
                _logger.LogInformation("Found existing claim ClaID: {ClaID} for patient PatID: {PatID}, DOS: {DOS}, Visit: {Visit}",
                    existingClaim.ClaID, patientId, firstServiceDate.Value, visitNumber ?? "N/A");
                return (existingClaim, false);
            }
        }

        // Resolve Billing Physician by NPI, then fall back to first active physician
        Physician? billingPhy = null;
        if (!string.IsNullOrWhiteSpace(hl7BillingNpi))
        {
            billingPhy = await _db.Physicians.FirstOrDefaultAsync(p => p.PhyNPI == hl7BillingNpi);
        }

        if (billingPhy == null)
        {
            billingPhy = await _db.Physicians
                .Where(p => !p.PhyInactive)
                .OrderBy(p => p.PhyID)
                .FirstOrDefaultAsync();
        }

        if (billingPhy == null)
        {
            throw new InvalidOperationException("HL7 Import Error: No physician available to assign to claim.");
        }

        // Resolve Attending Physician by NPI, then fall back to billing physician
        Physician? attendingPhy = null;
        if (!string.IsNullOrWhiteSpace(hl7AttendingNpi))
        {
            attendingPhy = await _db.Physicians.FirstOrDefaultAsync(p => p.PhyNPI == hl7AttendingNpi);
        }

        if (attendingPhy == null)
        {
            attendingPhy = billingPhy;
        }

        if (attendingPhy == null)
        {
            throw new InvalidOperationException("HL7 Import Error: No attending physician available to assign to claim.");
        }

        var newClaim = new Claim
        {
            ClaPatFID = patientId,
            ClaStatus = "Imported", // Default status for imported claims
            ClaSubmissionMethod = "Electronic", // HL7 imports are electronic
            ClaAdmittedDate = admittedDate,
            ClaDischargedDate = dischargedDate,
            ClaFirstDateTRIG = firstServiceDate,
            ClaMedicalRecordNumber = visitNumber, // Visit/Account number for deduplication
            ClaTotalChargeTRIG = 0,
            ClaTotalAmtPaidCC = 0,
            ClaTotalBalanceCC = 0,
            ClaTotalAmtAppliedCC = 0,
            ClaLocked = false,
            // Required fields with defaults
            ClaICDIndicator = "10", // Default to ICD-10
            ClaDiagnosisCodesCC = string.Empty,
            // Required foreign keys – resolved safely to satisfy FK constraints
            ClaBillingPhyFID = billingPhy.PhyID,
            ClaAttendingPhyFID = attendingPhy.PhyID,
            ClaFacilityPhyFID = billingPhy.PhyID,
            ClaOperatingPhyFID = attendingPhy.PhyID,
            ClaOrderingPhyFID = billingPhy.PhyID,
            ClaReferringPhyFID = billingPhy.PhyID,
            ClaRenderingPhyFID = billingPhy.PhyID,
            ClaSupervisingPhyFID = attendingPhy.PhyID
        };

        // Safety validation before inserting claim
        if (newClaim.ClaBillingPhyFID <= 0 || newClaim.ClaAttendingPhyFID <= 0)
        {
            throw new InvalidOperationException("HL7 Import Error: Invalid physician reference.");
        }

        _db.Claims.Add(newClaim);
        // Explicitly save the claim so ClaID is generated before any Service_Line uses SrvClaFID. Do not rely on EF ordering.
        await _db.SaveChangesAsync();

        if (newClaim.ClaID <= 0)
        {
            throw new InvalidOperationException($"Failed to create claim: ClaID is {newClaim.ClaID} after SaveChanges");
        }

        _logger.LogInformation("Created new claim ClaID: {ClaID} for patient PatID: {PatID}", newClaim.ClaID, patientId);
        return (newClaim, true);
    }

    /// <summary>
    /// Creates a Service_Line from an FT1 segment or returns existing if duplicate.
    /// Call only after Claim is persisted (SaveChangesAsync) so SrvClaFID exists in DB.
    /// EZClaim deduplication: Claim + CPT + DOS.
    /// </summary>
    /// <param name="claimId">ClaID of the already-persisted Claim</param>
    /// <param name="ft1Segment">Raw FT1 segment string</param>
    /// <param name="claimFirstDate">Claim first service date; used as fallback when FT1 date is missing</param>
    private async Task<(bool created, decimal charges)> CreateServiceLineFromFt1WithCharges(int claimId, string ft1Segment, DateOnly? claimFirstDate = null)
    {
        _logger.LogInformation("Creating ServiceLine with SrvClaFID {ClaimId}", claimId);

        if (claimId <= 0)
        {
            _logger.LogWarning("Cannot create service line: Invalid claim ID {ClaID}", claimId);
            return (false, 0m);
        }

        // Ensure Claim exists in DB before inserting Service_Line (avoid FK violation)
        var claimExists = await _db.Claims.AsNoTracking().AnyAsync(c => c.ClaID == claimId);
        if (!claimExists)
        {
            _logger.LogError("Claim missing before ServiceLine insert for ClaimId {ClaimId}", claimId);
            return (false, 0m);
        }

        if (string.IsNullOrWhiteSpace(ft1Segment))
        {
            _logger.LogWarning("Skipping FT1: segment is null or empty");
            return (false, 0m);
        }

        // FT1-4: Service date → SrvFromDate (fallback to claim date then today)
        var transactionDateStr = _parser.GetFieldValue(ft1Segment, 4);
        var serviceDate = _parser.ParseHl7Date(transactionDateStr)
            ?? claimFirstDate
            ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // FT1-7 or FT1-19: Procedure code (CPT) → SrvProcedureCode; missing CPT defaults to 99999
        var procedureCodePrimaryRaw = _parser.GetFieldValue(ft1Segment, 7);
        var procedureCodeAltRaw = _parser.GetFieldValue(ft1Segment, 19);
        var procedureCode = _parser.NormalizeString(
            !string.IsNullOrWhiteSpace(procedureCodePrimaryRaw) ? procedureCodePrimaryRaw : procedureCodeAltRaw,
            maxLength: 30);

        if (string.IsNullOrWhiteSpace(procedureCode))
        {
            _logger.LogWarning("FT1 missing procedure code. Using default CPT '99999'. Segment: {FT1}", ft1Segment);
            procedureCode = "99999";
        }

        // FT1-10: Units → SrvUnits
        var unitsStr = _parser.GetFieldValue(ft1Segment, 10);
        var units = float.TryParse(unitsStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var u) ? u : 0f;
        if (units <= 0)
        {
            units = 1f;
        }

        // FT1-11: Charges → SrvCharges (never null; ensure >= 0)
        var chargesStr = _parser.GetFieldValue(ft1Segment, 11);
        var charges = Math.Max(0m, _parser.ParseDecimal(chargesStr));

        _logger.LogInformation(
            "Parsed FT1 for claim {ClaimId}: ServiceDate={ServiceDate}, ProcedureCode={ProcedureCode}, Units={Units}, Charges={Charges}. Segment: {FT1}",
            claimId,
            serviceDate,
            procedureCode,
            units,
            charges,
            ft1Segment);

        // EZClaim deduplication: Match by Claim + CPT + DOS (AsNoTracking for lookup)
        if (!string.IsNullOrWhiteSpace(procedureCode))
        {
            var existingServiceLine = await _db.Service_Lines
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.SrvClaFID == claimId &&
                    s.SrvProcedureCode == procedureCode &&
                    s.SrvFromDate == serviceDate);

            if (existingServiceLine != null)
            {
                _logger.LogInformation("Found existing service line SrvID: {SrvID} for claim ClaID: {ClaID}, CPT: {CPT}, DOS: {DOS}",
                    existingServiceLine.SrvID, claimId, procedureCode, serviceDate);
                return (false, charges); // Skip duplicate but still count amount
            }
        }

        // FT1-13: Transaction Description → SrvDesc
        var description = _parser.GetFieldValue(ft1Segment, 13);

        // SrvFromDate/SrvToDate are DateOnly (value type, never null). SrvCharges >= 0. CPT default "99999".
        var serviceLine = new Service_Line
        {
            SrvClaFID = claimId,
            SrvFromDate = serviceDate,
            SrvToDate = serviceDate,
            SrvProcedureCode = procedureCode ?? "99999",
            SrvDesc = description,
            SrvCharges = charges,
            SrvUnits = units,
            SrvGUID = Guid.NewGuid(),
            // Required fields with defaults
            SrvModifiersCC = string.Empty,
            SrvResponsibleParty = 1,
            SrvSortTiebreaker = 0,
            SrvRespChangeDate = DateTime.UtcNow
        };

        await _db.Service_Lines.AddAsync(serviceLine);

        // Service line will be persisted when the caller saves the DbContext
        _logger.LogInformation("Service line created for claim {ClaimId}", claimId);
        return (true, charges);
    }

    /// <summary>
    /// Creates Claim_Insured records from IN1 segments
    /// Only processes primary (sequence 1) and secondary (sequence 2) insurance
    /// </summary>
    private async Task CreateClaimInsuredRecords(int claimId, int patientId, List<string> in1Segments)
    {
        if (claimId <= 0 || in1Segments == null || in1Segments.Count == 0)
        {
            return;
        }

        foreach (var in1Segment in in1Segments)
        {
            if (string.IsNullOrWhiteSpace(in1Segment))
            {
                continue;
            }

            // IN1-2: Insurance Plan ID (Payer ID)
            var payerIdStr = _parser.GetFieldValue(in1Segment, 2);
            // IN1-3: Insurance Company ID
            var insuranceCompanyId = _parser.GetFieldValue(in1Segment, 3);
            // IN1-4: Insurance Company Name
            var insuranceCompanyName = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 4), maxLength: 100);
            // IN1-8: Insurance Group Number
            var groupNumber = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 8), maxLength: 50);
            // IN1-9: Insurance Group Name
            var groupName = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 9), maxLength: 100);
            // IN1-10: Insured's Group Emp ID
            var insuredGroupEmpId = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 10), maxLength: 50);
            // IN1-11: Insured's Group Emp Name
            var insuredGroupEmpName = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 11), maxLength: 100);
            // IN1-15: Insurance Company Address (XAD format)
            var insuranceAddressField = _parser.GetFieldValue(in1Segment, 15);
            var (insuranceAddress, insuranceCity, insuranceState, insuranceZip) = _parser.ParseAddress(insuranceAddressField);
            
            // Override with individual fields if present (IN1-16, 17, 18 take precedence)
            var cityOverride = _parser.GetFieldValue(in1Segment, 16);
            if (!string.IsNullOrWhiteSpace(cityOverride))
            {
                insuranceCity = _parser.NormalizeString(cityOverride, maxLength: 50);
            }
            var stateOverride = _parser.GetFieldValue(in1Segment, 17);
            if (!string.IsNullOrWhiteSpace(stateOverride))
            {
                insuranceState = _parser.NormalizeStateCode(stateOverride);
            }
            var zipOverride = _parser.GetFieldValue(in1Segment, 18);
            if (!string.IsNullOrWhiteSpace(zipOverride))
            {
                insuranceZip = _parser.NormalizeString(zipOverride, maxLength: 20);
            }
            
            // IN1-19: Insurance Company Phone (XTN format)
            var insurancePhoneField = _parser.GetFieldValue(in1Segment, 19);
            var insurancePhone = _parser.SanitizePhoneNumber(insurancePhoneField, maxLength: 25);
            // IN1-36: Authorization Number
            var authorizationNumber = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 36), maxLength: 50);
            // IN1-49: Insurance Company Plan Type
            var planType = _parser.NormalizeString(_parser.GetFieldValue(in1Segment, 49), maxLength: 50);

            // Determine sequence (primary = 1, secondary = 2)
            // IN1-17 is typically the sequence, but we'll use position in list
            int sequence = in1Segments.IndexOf(in1Segment) + 1;
            if (sequence > 2) // Only process primary and secondary
            {
                continue;
            }

            // Find or create Payer (simplified - use insurance company name as payer name)
            // insuranceCompanyName is already normalized above
            var payer = await _db.Payers.FirstOrDefaultAsync(p => p.PayName == insuranceCompanyName);
            int payerId = 0;
            if (payer == null && !string.IsNullOrWhiteSpace(insuranceCompanyName))
            {
                // Create a basic payer record with sanitized values
                payer = new Payer
                {
                    PayName = insuranceCompanyName, // Already normalized
                    PayInactive = false,
                    PayAddr1 = _parser.NormalizeString(insuranceAddress, maxLength: 50),
                    PayCity = insuranceCity, // Already normalized
                    PayState = insuranceState, // Already normalized to 2-char
                    PayZip = insuranceZip, // Already normalized
                    PayPhoneNo = insurancePhone, // Already sanitized (digits only)
                    // Required fields with defaults
                    PayClaimType = "Professional",
                    PaySubmissionMethod = "Electronic",
                    PayEligibilityPhyID = 0,
                    PayNameWithInactiveCC = insuranceCompanyName, // Already normalized
                    PayCityStateZipCC = string.Empty
                };

                // Build city/state/zip composite
                var payerCityStateZipParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(insuranceCity)) payerCityStateZipParts.Add(insuranceCity);
                if (!string.IsNullOrWhiteSpace(insuranceState)) payerCityStateZipParts.Add(insuranceState);
                if (!string.IsNullOrWhiteSpace(insuranceZip)) payerCityStateZipParts.Add(insuranceZip);
                payer.PayCityStateZipCC = string.Join(", ", payerCityStateZipParts);

                await _db.Payers.AddAsync(payer);
                await _db.SaveChangesAsync();
            }
            payerId = payer?.PayID ?? 0;

            if (payerId == 0)
            {
                _logger.LogWarning("Cannot create Claim_Insured: No payer found or created for insurance company: {InsuranceCompany}", insuranceCompanyName);
                continue;
            }

            // Create Claim_Insured record with sanitized values
            var claimInsured = new Claim_Insured
            {
                ClaInsClaFID = claimId,
                ClaInsPatFID = patientId,
                ClaInsPayFID = payerId,
                ClaInsSequence = sequence,
                ClaInsGroupNumber = groupNumber, // Already normalized
                ClaInsPlanName = _parser.NormalizeString(groupName ?? planType, maxLength: 100),
                ClaInsIDNumber = insuredGroupEmpId, // Already normalized
                ClaInsPriorAuthorizationNumber = authorizationNumber, // Already normalized
                ClaInsAddress = _parser.NormalizeString(insuranceAddress, maxLength: 50),
                ClaInsCity = insuranceCity, // Already normalized
                ClaInsState = insuranceState, // Already normalized to 2-char
                ClaInsZip = insuranceZip, // Already normalized
                ClaInsPhone = insurancePhone, // Already sanitized (digits only)
                ClaInsRelationToInsured = 0, // Default to Self
                ClaInsSequenceDescriptionCC = sequence == 1 ? "Primary" : "Secondary",
                ClaInsCityStateZipCC = string.Empty
            };

            // Build city/state/zip composite
            var cityStateZipParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(insuranceCity)) cityStateZipParts.Add(insuranceCity);
            if (!string.IsNullOrWhiteSpace(insuranceState)) cityStateZipParts.Add(insuranceState);
            if (!string.IsNullOrWhiteSpace(insuranceZip)) cityStateZipParts.Add(insuranceZip);
            claimInsured.ClaInsCityStateZipCC = string.Join(", ", cityStateZipParts);

            await _db.Claim_Insureds.AddAsync(claimInsured);
            await _db.SaveChangesAsync();

            _logger.LogDebug("Created Claim_Insured for claim ClaID: {ClaID}, sequence: {Sequence}", claimId, sequence);
        }
    }

    /// <summary>
    /// Updates claim totals after all service lines are created
    /// Recalculates ClaTotalChargeTRIG and ClaTotalBalanceCC
    /// </summary>
    private async Task UpdateClaimTotals(int claimId)
    {
        // Get sum of all service line charges
        var totalCharge = await _db.Service_Lines
            .Where(s => s.SrvClaFID == claimId)
            .SumAsync(s => s.SrvCharges);

        // Update claim totals
        var claim = await _db.Claims.FindAsync(claimId);
        if (claim != null)
        {
            claim.ClaTotalChargeTRIG = totalCharge;
            // Balance = Charges - Payments - Adjustments (simplified for import)
            claim.ClaTotalBalanceCC = totalCharge - (claim.ClaTotalAmtPaidCC ?? 0) - (claim.ClaTotalAmtAppliedCC ?? 0);
            // ClaDateTimeModified set by global audit on SaveChanges

            await _db.SaveChangesAsync();
        }
    }
}
