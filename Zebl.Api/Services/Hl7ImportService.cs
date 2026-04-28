using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using PayerEntity = Zebl.Infrastructure.Persistence.Entities.Payer;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Services;

/// <summary>
/// Service for importing HL7 DFT messages into the database
/// Imports DFT messages: matches patients by MRN; reuses an existing claim when patient + first FT1 DOS + PV1 visit match.
/// </summary>
public class Hl7ImportService
{
    private readonly ZeblDbContext _db;
    private readonly ILogger<Hl7ImportService> _logger;
    private readonly Hl7ParserService _parser;
    private readonly IInboundContext _inboundContext;
    private readonly IClaimAuditService _claimAuditService;
    private readonly ClaimInitialStatusProvider _claimInitialStatus;

    // Debug-only behavior: when enabled, we will force creation paths to avoid “everything silently discarding” while you diagnose.
    // Set env var HL7_IMPORT_DEBUG_FORCE_CREATE=1 on the server to activate.
    private bool ForceCreateEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("HL7_IMPORT_DEBUG_FORCE_CREATE"), "1", StringComparison.OrdinalIgnoreCase);

    public Hl7ImportService(
        ZeblDbContext db,
        ILogger<Hl7ImportService> logger,
        Hl7ParserService parser,
        IInboundContext inboundContext,
        IClaimAuditService claimAuditService,
        ClaimInitialStatusProvider claimInitialStatus)
    {
        _db = db;
        _logger = logger;
        _parser = parser;
        _inboundContext = inboundContext;
        _claimAuditService = claimAuditService;
        _claimInitialStatus = claimInitialStatus;
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

        /// <summary>First N failure reasons (for API/UI); not every error if many messages fail.</summary>
        public List<string> ErrorMessages { get; } = new();
    }

    /// <summary>
    /// Processes a list of HL7 DFT messages and creates Patients, Claims, and Service_Lines.
    /// This is the ONLY place where a transaction is managed for HL7 imports.
    /// </summary>
    public async Task<Hl7ImportResult> ProcessHl7Messages(List<Hl7DftMessage> messages, string fileName = "unknown.hl7")
    {
        ArgumentNullException.ThrowIfNull(messages);
        var result = new Hl7ImportResult();
        var integrationId = _inboundContext.IntegrationId;
        var tenantId = _inboundContext.TenantId;
        var facilityId = _inboundContext.FacilityId;
        var forceCreate = ForceCreateEnabled;

        if (tenantId <= 0)
            throw new InvalidOperationException("Inbound tenant is required");

        if (facilityId <= 0)
            throw new InvalidOperationException("Inbound facility is required");

        _logger.LogInformation(
            "HL7 Import using Tenant={TenantId}, Facility={FacilityId}",
            tenantId,
            facilityId);

        _logger.LogInformation(
            "HL7 file import started for {FileName} with {MessageCount} parsed messages. IntegrationId={IntegrationId}, TenantId={TenantId}, FacilityId={FacilityId}",
            fileName, messages.Count, integrationId, tenantId, facilityId);
        _logger.LogInformation("HL7 import debug force-create enabled={ForceCreate}", forceCreate);
        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] ProcessHl7Messages: inbound messages in list={Count}, File={FileName}",
            messages.Count,
            fileName);

        // EnableRetryOnFailure requires user transactions to run inside CreateExecutionStrategy (per EF Core).
        var executionStrategy = _db.Database.CreateExecutionStrategy();

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];

            if (message == null)
            {
                result.Errors++;
                AddImportError(result, i, "Message reference is null.");
                _logger.LogWarning(
                    "HL7 DFT import: skipping message index {MessageIndex} — message reference is null. File={FileName}",
                    i,
                    fileName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(message.PidSegment))
            {
                _logger.LogWarning(
                    "HL7 message at index {MessageIndex} has missing/blank PID segment. File={FileName}. ForceCreate={ForceCreate}",
                    i, fileName, forceCreate);
            }

            var messageIndex = i;
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                var trackedBefore = _db.ChangeTracker.Entries().Count();

                try
                {
                    _logger.LogWarning(
                        "[HL7-IMPORT-DEBUG] Message index {MessageIndex}: begin transaction. ChangeTracker.Entries (before ProcessDftMessage)={Tracked}. File={FileName}",
                        messageIndex,
                        trackedBefore,
                        fileName);

                    var messageStats = await ProcessDftMessage(message, fileName, messageIndex);

                    _logger.LogInformation(
                        "HL7 DFT import: message index {MessageIndex} ProcessDftMessage finished. NewPatient={NewPatient}, NewClaim={NewClaim}, ServiceLines={Sl}, Amount={Amt}. File={FileName}",
                        messageIndex,
                        messageStats.NewPatient,
                        messageStats.NewClaim,
                        messageStats.ServiceLinesCreated,
                        messageStats.Amount,
                        fileName);

                    var flushed = await _db.SaveChangesAsync();
                    _logger.LogInformation(
                        "HL7 DFT import: pre-commit SaveChangesAsync wrote {Written} rows for message index {MessageIndex}. File={FileName}",
                        flushed,
                        messageIndex,
                        fileName);

                    await tx.CommitAsync();

                    result.SuccessCount++;
                    result.NewPatientsCount += messageStats.NewPatient ? 1 : 0;
                    result.NewClaimsCount += messageStats.NewClaim ? 1 : 0;
                    if (!messageStats.NewClaim)
                        result.DuplicateClaimsCount++;
                    result.NewServiceLinesCount += messageStats.ServiceLinesCreated;
                    result.TotalAmount += messageStats.Amount;

                    _logger.LogInformation(
                        "HL7 DFT import: message index {MessageIndex} committed. Success so far={Ok}, new claims so far={Claims}. File={FileName}",
                        messageIndex,
                        result.SuccessCount,
                        result.NewClaimsCount,
                        fileName);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    AddImportError(result, messageIndex, $"{ex.GetType().Name}: {ex.Message}");
                    _logger.LogError(
                        ex,
                        "HL7 DFT import failed for message index {MessageIndex}; rolling back this message only and continuing file. File={FileName}",
                        messageIndex,
                        fileName);

                    try
                    {
                        await tx.RollbackAsync();
                    }
                    catch (Exception rbEx)
                    {
                        _logger.LogWarning(rbEx, "HL7 DFT import: rollback after message failure also threw. MessageIndex={MessageIndex}", messageIndex);
                    }

                    _db.ChangeTracker.Clear();
                }
            });
        }

        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] ProcessHl7Messages END: SuccessCount={Ok}, NewPatients={Np}, NewClaims={Nc}, NewServiceLines={Sl}. File={FileName}",
            result.SuccessCount,
            result.NewPatientsCount,
            result.NewClaimsCount,
            result.NewServiceLinesCount,
            fileName);

        return result;
    }

    private static void AddImportError(Hl7ImportResult result, int messageIndex, string detail)
    {
        const int maxMessages = 20;
        if (result.ErrorMessages.Count >= maxMessages)
            return;
        var text = $"Message {messageIndex + 1}: {detail}";
        if (text.Length > 500)
            text = text[..497] + "...";
        result.ErrorMessages.Add(text);
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
    private async Task<MessageStats> ProcessDftMessage(Hl7DftMessage message, string fileName, int messageIndex)
    {
        var stats = new MessageStats();

        // Extract key values for logging BEFORE any DB operations.
        var pidSegmentForExtraction = message.PidSegment ?? string.Empty;
        var pv1SegmentForExtraction = message.Pv1Segment ?? string.Empty;
        var pv1Has = !string.IsNullOrWhiteSpace(message.Pv1Segment);
        var pidHas = !string.IsNullOrWhiteSpace(message.PidSegment);

        var patientMrnForLog = _parser.ExtractPatientMrn(pidSegmentForExtraction);
        var (patientFirstNameForLog, patientLastNameForLog) = _parser.ExtractPatientName(pidSegmentForExtraction);

        // Insurance: log first IN1 company name and count.
        string? insuranceCompanyNameForLog = null;
        if (message.In1Segments != null && message.In1Segments.Count > 0)
        {
            var in1First = message.In1Segments[0];
            insuranceCompanyNameForLog = _parser.NormalizeString(_parser.GetFieldValue(in1First, 4), maxLength: 100);
        }

        // Physician: log PV1 billing/referring and attending.
        string? attendingNpiForLog = null;
        string? billingNpiForLog = null;
        if (!string.IsNullOrWhiteSpace(message.Pv1Segment))
        {
            var attendingFieldForLog = _parser.GetFieldValue(pv1SegmentForExtraction, 7);
            var referringFieldForLog = _parser.GetFieldValue(pv1SegmentForExtraction, 8);
            var (attNpiLog, _, _) = ParsePv1Xcn(attendingFieldForLog);
            var (refNpiLog, _, _) = ParsePv1Xcn(referringFieldForLog);
            attendingNpiForLog = attNpiLog;
            billingNpiForLog = refNpiLog ?? attNpiLog;
        }

        _logger.LogInformation(
            "HL7 DFT message index {MessageIndex} extracted: PIDPresent={PidPresent}, MRN={Mrn}, PatientName={FirstName} {LastName}, PV1Present={Pv1Present}, AttendingNPI={AttendingNpi}, BillingNPI={BillingNpi}, IN1Count={In1Count}, InsuranceCompany={InsuranceCompany}, FT1Count={Ft1Count}. File={FileName}",
            messageIndex,
            pidHas,
            patientMrnForLog ?? "null",
            patientFirstNameForLog,
            patientLastNameForLog,
            pv1Has,
            attendingNpiForLog ?? "null",
            billingNpiForLog ?? "null",
            message.In1Segments?.Count ?? 0,
            insuranceCompanyNameForLog ?? "null",
            message.Ft1Segments?.Count ?? 0,
            fileName);

        // Step 1: Extract patient MRN from PID-3
        var mrn = _parser.ExtractPatientMrn(pidSegmentForExtraction);
        if (string.IsNullOrWhiteSpace(mrn))
        {
            mrn = $"HL7_AUTO_{Guid.NewGuid():N}";
            Console.WriteLine("HL7 warning: missing data - PID-3 MRN missing, generated fallback MRN");
        }

        // Step 2: Match or create Patient
        var (patient, isNewPatient) = await MatchOrCreatePatient(pidSegmentForExtraction, mrn);
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
            Console.WriteLine("HL7 warning: missing data - claim creation failed for message");
            return stats;
        }
        stats.NewClaim = isNewClaim;
        Console.WriteLine(
            stats.NewClaim
                ? $"[HL7] New claim {claim.ClaID} for {patientFirstNameForLog} {patientLastNameForLog}"
                : $"[HL7] Reusing existing claim {claim.ClaID} for {patientFirstNameForLog} {patientLastNameForLog}");

        // Claim is now in the database; claim.ClaID is set.
        _logger.LogInformation("HL7 Claim persisted with ClaID {ClaimId}; creating insurance rows and payers before service lines.", claim.ClaID);

        // Step 4a: Claim_Insured + Payers (only for newly created claims — avoid duplicate primary/secondary rows on re-import).
        if (stats.NewClaim)
        {
            await CreateClaimInsuredRecords(claim.ClaID, patient.PatID, message.In1Segments);
        }

        if (stats.NewClaim)
        {
            try
            {
                await _claimAuditService.AddInsuranceEditedAsync(claim.ClaID);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Claim_Audit AddInsuranceEdited failed for claim {ClaId} (non-fatal).", claim.ClaID);
            }
        }

        var primaryPayerId = await _db.Claim_Insureds.AsNoTracking()
            .Where(ci => ci.ClaInsClaFID == claim.ClaID && ci.ClaInsSequence == 1)
            .Select(ci => ci.ClaInsPayFID)
            .FirstOrDefaultAsync();
        if (primaryPayerId <= 0)
        {
            var fallbackPayer = await EnsurePayerExistsAsync("SELF PAY", null, null, null, null, null);
            primaryPayerId = fallbackPayer.PayID;
            Console.WriteLine("HL7 warning: missing data - no primary payer row, using SELF PAY");
        }

        var claimFirstDate = claim.ClaFirstDateTRIG ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Step 4b: Iterate FT1 segments and add Service_Line entities (no SaveChanges in loop).
        int serviceLinesCreated = 0;
        decimal amount = 0m;
        foreach (var ft1Segment in message.Ft1Segments ?? new List<string>())
        {
            var (created, charges) = await CreateServiceLineFromFt1WithCharges(claim.ClaID, ft1Segment, claimFirstDate, primaryPayerId);
            if (created) serviceLinesCreated++;
            else _logger.LogDebug("Skipped FT1 segment for claim {ClaimId}: duplicate or invalid. Segment: {FT1}", claim.ClaID, ft1Segment);
            amount += charges;
        }

        if (serviceLinesCreated == 0)
        {
            if (stats.NewClaim)
            {
                _logger.LogWarning("No FT1 service lines detected for claim {ClaID}. Creating default HL7 service line.", claim.ClaID);
                var defaultFt1 = $"FT1|1|||{claimFirstDate:yyyyMMdd}||||||DEFAULT|1";
                var (created, charges) = await CreateServiceLineFromFt1WithCharges(claim.ClaID, defaultFt1, claimFirstDate, primaryPayerId);
                if (created)
                {
                    serviceLinesCreated++;
                    amount += charges;
                }
            }
            else
            {
                _logger.LogInformation(
                    "HL7 re-import: no new FT1 lines for existing claim {ClaID}; skipping default placeholder line.",
                    claim.ClaID);
            }
        }

        // Step 5: Persist all Service_Lines in one call (Claim was already saved in Step 3).
        if (serviceLinesCreated > 0)
        {
            var written = await _db.SaveChangesAsync();
            _logger.LogInformation("HL7 DFT message index {MessageIndex}: Service_Line SaveChanges wrote {Written} rows. ClaimId={ClaimId}", messageIndex, written, claim.ClaID);
        }

        stats.ServiceLinesCreated = serviceLinesCreated;
        stats.Amount = amount;

        // Step 6: Update claim totals after all service lines are created
        await UpdateClaimTotals(claim.ClaID);

        // Step 7: Insert Claim_Audit (shows in Claim Note List with filename)
        var noteText = stats.NewClaim
            ? $"Claim Note: Imported from file {fileName}."
            : $"Claim Note: HL7 re-import from file {fileName} merged into existing claim (duplicate visit/DOS).";
        var activityType = stats.NewClaim ? "Claim Imported" : "Claim Updated";
        try
        {
            var claimForSnapshot = await _db.Claims.AsNoTracking()
                .Where(c => c.ClaID == claim.ClaID)
                .Select(c => new { c.ClaTotalChargeTRIG, c.ClaTotalInsBalanceTRIG, c.ClaTotalPatBalanceTRIG })
                .FirstOrDefaultAsync();
            _db.Claim_Audits.Add(new Claim_Audit
            {
                TenantId = claim.TenantId,
                FacilityId = claim.FacilityId,
                ClaFID = claim.ClaID,
                ActivityType = activityType,
                ActivityDate = DateTime.UtcNow,
                UserName = "SYSTEM",
                ComputerName = Environment.MachineName,
                Notes = noteText,
                TotalCharge = claimForSnapshot?.ClaTotalChargeTRIG,
                InsuranceBalance = claimForSnapshot?.ClaTotalInsBalanceTRIG,
                PatientBalance = claimForSnapshot?.ClaTotalPatBalanceTRIG
            });
            var written = await _db.SaveChangesAsync();
            _logger.LogInformation("HL7 DFT message index {MessageIndex}: Claim_Audit SaveChanges wrote {Written} rows. ClaimId={ClaimId}", messageIndex, written, claim.ClaID);
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
        var tenantId = _inboundContext.TenantId;
        var facilityId = _inboundContext.FacilityId;
        if (tenantId <= 0 || facilityId <= 0)
            throw new InvalidOperationException("Inbound integration tenant/facility context is required.");

        // Normalize MRN (trim, truncate to max length)
        var normalizedMrn = _parser.NormalizeString(mrn, maxLength: 50);
        if (string.IsNullOrWhiteSpace(normalizedMrn))
        {
            normalizedMrn = _parser.NormalizeString($"HL7_AUTO_{Guid.NewGuid():N}", maxLength: 50);
            Console.WriteLine("HL7 warning: missing data - MRN invalid after normalization, generated fallback MRN");
        }

        // Match by PatAccountNo (MRN)
        var existingPatient = await _db.Patients
            .FirstOrDefaultAsync(p =>
                p.PatAccountNo == normalizedMrn &&
                p.TenantId == tenantId &&
                p.FacilityId == facilityId);

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

        // Patient rows require NOT NULL physician FKs.
        var defaultPhysician = await _db.Physicians
            .Where(p => p.TenantId == tenantId && p.FacilityId == facilityId)
            .OrderBy(p => p.PhyID)
            .FirstOrDefaultAsync();

        if (defaultPhysician == null)
        {
            var placeholderPhysician = new Physician
            {
                PhyNPI = $"IMPORT_PLACEHOLDER_{Guid.NewGuid():N}".Substring(0, 15),
                PhyName = "HL7 Placeholder",
                PhyFirstName = "HL7",
                PhyLastName = "Placeholder",
                TenantId = tenantId,
                FacilityId = facilityId,
                PhyType = "Rendering",
                PhyInactive = false,
                PhyDateTimeCreated = DateTime.UtcNow
            };
            await _db.Physicians.AddAsync(placeholderPhysician);
            await _db.SaveChangesAsync();
            defaultPhysician = placeholderPhysician;
            Console.WriteLine("HL7 warning: missing data - no default physician found, created placeholder physician");
        }

        var defaultPhysicianId = defaultPhysician.PhyID;
        _logger.LogInformation(
            "Using physician {PhyID} as default for HL7 patient import",
            defaultPhysicianId);

        // Create new patient with all NOT NULL columns populated (EZClaim defaults)
        var newPatient = new Patient
        {
            TenantId = tenantId,
            FacilityId = facilityId,
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
        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] MatchOrCreatePatient: about to SaveChangesAsync for new Patient MRN={Mrn}",
            normalizedMrn);
        var patientWritten = await _db.SaveChangesAsync(); // Save to get PatID
        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] MatchOrCreatePatient: SaveChangesAsync AffectedRows={Written}, PatID={PatID}",
            patientWritten,
            newPatient.PatID);
        _logger.LogInformation("MatchOrCreatePatient: Patient SaveChanges wrote {Written} rows. MRN={Mrn}, Tenant={TenantId}, Facility={FacilityId}", patientWritten, normalizedMrn, tenantId, facilityId);

        if (newPatient.PatID <= 0)
        {
            throw new InvalidOperationException($"Failed to create patient: PatID is {newPatient.PatID} after SaveChanges");
        }

        _logger.LogInformation("Created new patient with MRN {MRN}, PatID: {PatID}", normalizedMrn, newPatient.PatID);
        return (newPatient, true);
    }

    /// <summary>
    /// Returns an existing claim when the same patient already has a claim for this first FT1 DOS and PV1 visit (unless force-create debug is on);
    /// otherwise creates a new claim, saves it, and returns it with isNew true.
    /// </summary>
    private async Task<(Claim claim, bool isNew)> CreateNewClaim(int patientId, Hl7DftMessage message)
    {
        var tenantId = _inboundContext.TenantId;
        var facilityId = _inboundContext.FacilityId;
        if (tenantId <= 0 || facilityId <= 0)
            throw new InvalidOperationException("Inbound integration tenant/facility context is required.");

        if (patientId <= 0)
        {
            throw new ArgumentException($"Invalid patient ID: {patientId}", nameof(patientId));
        }

        // Default Bill-To for imported claims:
        // - When IN1 exists, there is primary insurance → Primary (1)
        // - When IN1 is missing, treat as no insurance/self-pay → Patient (0)
        var hasIn1Segments = message.In1Segments != null && message.In1Segments.Count > 0;
        var defaultBillTo = hasIn1Segments ? (int)ClaimBillTo.Primary : (int)ClaimBillTo.Patient;

        var patient = await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.PatID == patientId &&
                p.TenantId == tenantId &&
                p.FacilityId == facilityId);
        if (patient == null)
        {
            throw new InvalidOperationException(
                $"Cannot create claim. Patient {patientId} is outside current tenant/facility scope.");
        }

        // Extract PV1 data if available
        DateOnly? admittedDate = null;
        DateOnly? dischargedDate = null;
        string? visitNumber = null;
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
        }

        // EZClaim style: use first meaningful physician candidate from PV1 physician slots.
        var pv1Seg = message.Pv1Segment ?? string.Empty;
        var attendingField = GetPhysicianFromPv1(pv1Seg);
        var referringField = _parser.GetFieldValue(pv1Seg, 8) ?? _parser.GetFieldValue(pv1Seg, 9) ?? attendingField;
        var nm1_82 = message.Nm1Segments?
            .FirstOrDefault(s => string.Equals(_parser.GetFieldValue(s, 1), "82", StringComparison.OrdinalIgnoreCase));
        string? nm1Npi = null;
        string? nm1Last = null;
        string? nm1First = null;
        if (!string.IsNullOrWhiteSpace(nm1_82))
        {
            var fields = nm1_82.Split('|', StringSplitOptions.None);
            nm1Last = fields.Length > 3 ? _parser.NormalizeString(fields[3], maxLength: 60) : null;
            nm1First = fields.Length > 4 ? _parser.NormalizeString(fields[4], maxLength: 35) : null;
            nm1Npi = fields.Length > 9 ? _parser.NormalizeString(fields[9], maxLength: 20) : null;
        }
        var (pv1AttNpi, pv1AttLast, pv1AttFirst) = ParseXcn(attendingField);
        var attNpi = !string.IsNullOrWhiteSpace(nm1Npi) ? nm1Npi : pv1AttNpi;
        var attLast = !string.IsNullOrWhiteSpace(nm1Last) ? nm1Last : pv1AttLast;
        var attFirst = !string.IsNullOrWhiteSpace(nm1First) ? nm1First : pv1AttFirst;
        var (refNpi, refLast, refFirst) = ParseXcn(referringField);
        if (string.IsNullOrWhiteSpace(attFirst) && string.IsNullOrWhiteSpace(attLast))
        {
            attFirst = attNpi;
            attLast = "HL7";
            Console.WriteLine("HL7 warning: missing data - physician name missing, using ID fallback");
        }
        Console.WriteLine($"[HL7 IMPORT] Tenant={tenantId}, Facility={facilityId}");
        Console.WriteLine($"[HL7 IMPORT] Physician={attFirst} {attLast}");

        // Get first service date from FT1 segments for claim date range (DOS)
        DateOnly? firstServiceDate = null;
        if (message.Ft1Segments.Count > 0)
        {
            var firstFt1 = message.Ft1Segments[0];
            // FT1-4: Transaction Date (service date) - this is the DOS
            var transactionDateStr = _parser.GetFieldValue(firstFt1, 4);
            firstServiceDate = _parser.ParseHl7Date(transactionDateStr);
        }

        // Same dedupe key as HL7 review: patient + first DOS + PV1 visit/account (ClaMedicalRecordNumber).
        if (!ForceCreateEnabled && firstServiceDate.HasValue)
        {
            var normVisit = NormalizeHl7VisitKey(visitNumber);
            var existingClaim = await _db.Claims
                .Where(c => c.TenantId == tenantId && c.FacilityId == facilityId && c.ClaPatFID == patientId)
                .Where(c => c.ClaFirstDateTRIG == firstServiceDate)
                .Where(c => normVisit == null
                    ? c.ClaMedicalRecordNumber == null || c.ClaMedicalRecordNumber == ""
                    : c.ClaMedicalRecordNumber == normVisit)
                .OrderByDescending(c => c.ClaID)
                .FirstOrDefaultAsync();
            if (existingClaim != null)
            {
                _logger.LogInformation(
                    "HL7 DFT: reusing existing claim ClaID={ClaId} for patient PatID={PatId}, DOS={Dos}, visit={Visit} (duplicate import).",
                    existingClaim.ClaID,
                    patientId,
                    firstServiceDate,
                    normVisit ?? "(none)");

                // If legacy data has NULL ClaBillTo, deterministically default based on IN1 presence.
                if (existingClaim.ClaBillTo == null)
                    existingClaim.ClaBillTo = defaultBillTo;
                return (existingClaim, false);
            }
        }

        // Attending always from PV1-7; billing/referring from PV1-8 when present, else same as attending (no random First()).
        var attendingPhy = await FindOrCreatePhysicianFromHl7Pv1Async(
            attNpi,
            attFirst,
            attLast,
            "Attending",
            tenantId,
            facilityId);

        Physician billingPhy;
        var referringMeaningful = !string.IsNullOrWhiteSpace(referringField) &&
            (!string.IsNullOrWhiteSpace(refNpi) ||
             !string.IsNullOrWhiteSpace(refLast) ||
             !string.IsNullOrWhiteSpace(refFirst));
        if (!referringMeaningful)
        {
            billingPhy = attendingPhy;
        }
        else
        {
            var billNpi = string.IsNullOrWhiteSpace(refNpi) ? attNpi : refNpi;
            var billFirst = string.IsNullOrWhiteSpace(refFirst) ? attFirst : refFirst;
            var billLast = string.IsNullOrWhiteSpace(refLast) ? attLast : refLast;
            billingPhy = await FindOrCreatePhysicianFromHl7Pv1Async(
                billNpi,
                billFirst,
                billLast,
                "Referring/Billing",
                tenantId,
                facilityId);
        }

        var initialClaStatus = await _claimInitialStatus.GetInitialClaStatusStringAsync();
        Console.WriteLine("DEFAULT STATUS FROM SETTINGS: " + initialClaStatus);

        var newClaim = new Claim
        {
            TenantId = tenantId,
            FacilityId = facilityId,
            ClaPatFID = patientId,
            ClaStatus = initialClaStatus,
            ClaSubmissionMethod = "Electronic", // HL7 imports are electronic
            ClaBillTo = defaultBillTo,
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
        Console.WriteLine("FINAL CLAIM STATUS: " + newClaim.ClaStatus);
        if (newClaim.TenantId != patient.TenantId)
        {
            Console.WriteLine("HL7 warning: missing data - tenant mismatch on claim/patient");
        }

        if (newClaim.ClaBillingPhyFID <= 0 || newClaim.ClaAttendingPhyFID <= 0)
        {
            Console.WriteLine("HL7 warning: missing data - physician reference invalid, using patient defaults");
            newClaim.ClaBillingPhyFID = patient.PatBillingPhyFID;
            newClaim.ClaAttendingPhyFID = patient.PatReferringPhyFID;
        }

        _db.Claims.Add(newClaim);
        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] CreateNewClaim: about to SaveChangesAsync for new Claim (PatientId={PatientId}, TenantId={TenantId}, FacilityId={FacilityId}). Tracked claim entity state=Added.",
            patientId,
            tenantId,
            facilityId);
        // Explicitly save the claim so ClaID is generated before any Service_Line uses SrvClaFID. Do not rely on EF ordering.
        var claimWritten = await _db.SaveChangesAsync();
        _logger.LogWarning(
            "[HL7-IMPORT-DEBUG] CreateNewClaim: SaveChangesAsync returned AffectedRows={Written}, new ClaID={ClaID}",
            claimWritten,
            newClaim.ClaID);
        _logger.LogInformation("CreateNewClaim: Claim SaveChanges wrote {Written} rows. PatientId={PatientId}, ClaimTempTenant={TenantId}, ClaimTempFacility={FacilityId}", claimWritten, patientId, tenantId, facilityId);

        // These logs confirm which physician IDs were linked to the created Claim.
        _logger.LogWarning(
            "DFT Import → Linked Physicians to Claim: BillingPhyID={BillingPhyID}, AttendingPhyID={AttendingPhyID}",
            billingPhy.PhyID,
            attendingPhy.PhyID);

        if (newClaim.ClaID <= 0)
        {
            throw new InvalidOperationException($"Failed to create claim: ClaID is {newClaim.ClaID} after SaveChanges");
        }

        _logger.LogInformation("Created new claim ClaID: {ClaID} for patient PatID: {PatID}", newClaim.ClaID, patientId);
        return (newClaim, true);
    }

    /// <summary>Normalize PV1-19 visit / account number so "" and null match the same as in HL7 review.</summary>
    private static string? NormalizeHl7VisitKey(string? visitNumber) =>
        string.IsNullOrWhiteSpace(visitNumber) ? null : visitNumber.Trim();

    /// <summary>
    /// Creates a Service_Line from an FT1 segment or returns existing if duplicate.
    /// Call only after Claim is persisted (SaveChangesAsync) so SrvClaFID exists in DB.
    /// EZClaim deduplication: Claim + CPT + DOS.
    /// </summary>
    /// <param name="claimId">ClaID of the already-persisted Claim</param>
    /// <param name="ft1Segment">Raw FT1 segment string</param>
    /// <param name="claimFirstDate">Claim first service date; used as fallback when FT1 date is missing</param>
    private async Task<(bool created, decimal charges)> CreateServiceLineFromFt1WithCharges(
        int claimId,
        string ft1Segment,
        DateOnly? claimFirstDate,
        int responsiblePartyPayerId)
    {
        var tenantId = _inboundContext.TenantId;
        var facilityId = _inboundContext.FacilityId;
        var forceCreate = ForceCreateEnabled;
        if (tenantId <= 0)
            throw new InvalidOperationException("Inbound integration tenant context is required.");
        if (facilityId <= 0)
            throw new InvalidOperationException("Inbound integration facility context is required.");

        _logger.LogInformation("Creating ServiceLine with SrvClaFID {ClaimId}", claimId);

        if (claimId <= 0)
        {
            _logger.LogWarning("Cannot create service line: Invalid claim ID {ClaID}", claimId);
            return (false, 0m);
        }

        // Ensure Claim exists in DB before inserting Service_Line (avoid FK violation)
        var claimScope = await _db.Claims.AsNoTracking()
            .Where(c => c.ClaID == claimId)
            .Select(c => new { c.TenantId, c.FacilityId })
            .FirstOrDefaultAsync();
        if (claimScope == null)
        {
            _logger.LogError("Claim missing before ServiceLine insert for ClaimId {ClaimId}", claimId);
            return (false, 0m);
        }
        if (claimScope.TenantId != tenantId || claimScope.FacilityId != facilityId)
            throw new InvalidOperationException("Inbound integration tenant/facility context does not match Claim scope.");

        if (responsiblePartyPayerId <= 0)
            throw new InvalidOperationException("Responsible party payer id must be positive for HL7 service line import (FK_ServiceLine_ResponsibleParty).");

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
                    s.TenantId == tenantId &&
                    s.FacilityId == facilityId &&
                    s.SrvProcedureCode == procedureCode &&
                    s.SrvFromDate == serviceDate);

            if (existingServiceLine != null)
            {
                _logger.LogInformation("Found existing service line SrvID: {SrvID} for claim ClaID: {ClaID}, CPT: {CPT}, DOS: {DOS}",
                    existingServiceLine.SrvID, claimId, procedureCode, serviceDate);
                if (!forceCreate)
                {
                    return (false, charges); // Skip duplicate but still count amount
                }

                // Debug force-create: ignore dedup and insert another row.
                _logger.LogWarning("ForceCreate enabled: existing service line found (SrvID={SrvID}); inserting duplicate row for claim {ClaimId}.", existingServiceLine.SrvID, claimId);
            }
        }

        // FT1-13: Transaction Description → SrvDesc
        var description = _parser.GetFieldValue(ft1Segment, 13);

        // SrvFromDate/SrvToDate are DateOnly (value type, never null). SrvCharges >= 0. CPT default "99999".
        var serviceLine = new Service_Line
        {
            TenantId = tenantId,
            FacilityId = facilityId,
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
            SrvResponsibleParty = responsiblePartyPayerId,
            SrvSortTiebreaker = 0,
            SrvRespChangeDate = DateTime.UtcNow
        };

        await _db.Service_Lines.AddAsync(serviceLine);

        // Service line will be persisted when the caller saves the DbContext
        _logger.LogInformation("Service line created for claim {ClaimId}", claimId);
        return (true, charges);
    }

    /// <summary>
    /// Creates Claim_Insured records from IN1 segments; auto-creates payers when missing (empty DB safe).
    /// </summary>
    private async Task CreateClaimInsuredRecords(int claimId, int patientId, List<string>? in1Segments)
    {
        _logger.LogInformation(
            "CreateClaimInsuredRecords: claimId={ClaimId}, patientId={PatientId}, IN1Count={In1Count}",
            claimId,
            patientId,
            in1Segments?.Count ?? 0);

        if (claimId <= 0)
        {
            _logger.LogWarning(
                "CreateClaimInsuredRecords: invalid claimId={ClaimId} — cannot create insurance rows.",
                claimId);
            return;
        }

        var segments = in1Segments ?? new List<string>();

        if (segments.Count == 0)
        {
            var payer = await EnsurePayerExistsAsync("SELF PAY", null, null, null, null, null);
            await AddClaimInsuredRowAsync(
                claimId,
                patientId,
                payer.PayID,
                sequence: 1,
                groupNumber: null,
                planName: null,
                insuredId: null,
                authNumber: null,
                address: null,
                city: null,
                state: null,
                zip: null,
                phone: null);
            Console.WriteLine("HL7 warning: missing data - no IN1 segments, used SELF PAY");
            return;
        }

        for (var in1Index = 0; in1Index < segments.Count; in1Index++)
        {
            var rawSegment = segments[in1Index];
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                Console.WriteLine($"HL7 warning: missing data - blank IN1 at index {in1Index}");
                continue;
            }

            var groupNumber = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 8), maxLength: 50);
            var groupName = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 9), maxLength: 100);
            var insuredGroupEmpId = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 10), maxLength: 50);
            var authorizationNumber = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 36), maxLength: 50);
            var planType = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 49), maxLength: 50);

            // Insurance company (payer): IN1-4 name, IN1-5 address (XAD), IN1-7 phone (XTN) — HL7 v2.x
            var payerName = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 4), maxLength: 50);
            if (string.IsNullOrWhiteSpace(payerName))
            {
                payerName = _parser.NormalizeString(_parser.GetFieldValue(rawSegment, 3), maxLength: 50);
            }
            if (string.IsNullOrWhiteSpace(payerName))
            {
                payerName = "SELF PAY";
                Console.WriteLine("HL7 warning: missing data - IN1 payer name missing, using SELF PAY");
            }
            Console.WriteLine($"[HL7 IMPORT] Tenant={_inboundContext.TenantId}, Facility={_inboundContext.FacilityId}");
            Console.WriteLine($"[HL7 IMPORT] Payer={payerName}");

            var addressField = _parser.GetFieldValue(rawSegment, 5);
            var (addr1, city, state, zip) = _parser.ParseAddress(addressField);
            state = _parser.NormalizeStateCode(state);
            if (!string.IsNullOrWhiteSpace(zip))
            {
                zip = new string(zip.Where(char.IsDigit).ToArray()).Trim();
                if (zip.Length > 10)
                    zip = zip.Substring(0, 10);
            }
            else
            {
                zip = null;
            }

            var phoneField = _parser.GetFieldValue(rawSegment, 7);
            var phone = _parser.SanitizePhoneNumber(phoneField, maxLength: 25);

            addr1 = _parser.NormalizeString(addr1, maxLength: 50);
            city = _parser.NormalizeString(city, maxLength: 50);
            zip = string.IsNullOrWhiteSpace(zip) ? null : zip;

            if (city != null && city.Contains('^', StringComparison.Ordinal))
                city = city.Replace("^", " ");

            if (zip != null && zip.Length > 10)
                zip = zip.Substring(0, 10);

            if (phone != null && phone.Length < 7)
                phone = null;

            var normalizedName = payerName.Trim().ToUpperInvariant();

            _logger.LogError(
                "PAYER DEBUG → Name={Name}, City={City}, State={State}, Zip={Zip}, Phone={Phone}",
                payerName,
                city,
                state,
                zip,
                phone);

            var payer = await EnsurePayerExistsAsync(
                normalizedName,
                addr1,
                city,
                state,
                zip,
                phone);

            // Insured subscriber address on claim: IN1-19 (XAD) — not merged into Payer
            var insuredAddressField = _parser.GetFieldValue(rawSegment, 19);
            var (claAddr, claCity, claState, claZip) = _parser.ParseAddress(insuredAddressField);
            claState = _parser.NormalizeStateCode(claState);
            if (!string.IsNullOrWhiteSpace(claZip))
            {
                claZip = new string(claZip.Where(char.IsDigit).ToArray()).Trim();
                if (claZip.Length > 10)
                    claZip = claZip.Substring(0, 10);
            }
            else
            {
                claZip = null;
            }
            claAddr = _parser.NormalizeString(claAddr, maxLength: 50);
            claCity = _parser.NormalizeString(claCity, maxLength: 50);
            claZip = string.IsNullOrWhiteSpace(claZip) ? null : claZip;
            if (claCity != null && claCity.Contains('^', StringComparison.Ordinal))
                claCity = null;
            if (claZip != null && claZip.Length > 10)
                claZip = null;
            string? claPhone = null;

            var sequence = in1Index + 1;
            if (sequence > 2)
            {
                _logger.LogWarning(
                    "CreateClaimInsuredRecords: sequence {Seq} > 2; clamping to 2. claimId={ClaimId}",
                    sequence,
                    claimId);
                sequence = 2;
            }

            await AddClaimInsuredRowAsync(
                claimId,
                patientId,
                payer.PayID,
                sequence,
                groupNumber,
                _parser.NormalizeString(groupName ?? planType, maxLength: 100),
                insuredGroupEmpId,
                authorizationNumber,
                claAddr,
                claCity,
                claState,
                claZip,
                claPhone);

            _logger.LogInformation(
                "CreateClaimInsuredRecords: added Claim_Insured claimId={ClaimId}, sequence={Sequence}, payerId={PayerId}, payerName={PayerName}",
                claimId,
                sequence,
                payer.PayID,
                normalizedName);
        }
    }

    private async Task<PayerEntity> EnsurePayerExistsAsync(
        string normalizedName,
        string? addr1,
        string? city,
        string? state,
        string? zip,
        string? phone)
    {
        var tenantId = _inboundContext.TenantId;
        var facilityId = _inboundContext.FacilityId;

        var payer = await _db.Payers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
            p.PayName == normalizedName &&
            p.TenantId == tenantId &&
            p.FacilityId == facilityId);

        if (payer != null)
            return payer;

        var payerCityStateZipParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(city)) payerCityStateZipParts.Add(city);
        if (!string.IsNullOrWhiteSpace(state)) payerCityStateZipParts.Add(state);
        if (!string.IsNullOrWhiteSpace(zip)) payerCityStateZipParts.Add(zip);

        payer = new PayerEntity
        {
            PayName = normalizedName,
            TenantId = tenantId,
            FacilityId = facilityId,
            PayInactive = false,
            PayAddr1 = addr1,
            PayCity = city,
            PayState = state,
            PayZip = zip,
            PayPhoneNo = phone,
            PayClaimType = "Professional",
            PaySubmissionMethod = "Electronic",
            PayEligibilityPhyID = 0,
            PayNameWithInactiveCC = normalizedName,
            PayCityStateZipCC = string.Join(", ", payerCityStateZipParts)
        };

        _logger.LogWarning("DFT Import → Creating Payer: {Name}", normalizedName);
        await _db.Payers.AddAsync(payer);
        var written = await _db.SaveChangesAsync();
        _logger.LogWarning("DFT Import → Payer Saved with ID: {Id} (rows={Written})", payer.PayID, written);
        return payer;
    }

    private async Task AddClaimInsuredRowAsync(
        int claimId,
        int patientId,
        int payerId,
        int sequence,
        string? groupNumber,
        string? planName,
        string? insuredId,
        string? authNumber,
        string? address,
        string? city,
        string? state,
        string? zip,
        string? phone)
    {
        var claimInsured = new Claim_Insured
        {
            ClaInsClaFID = claimId,
            ClaInsPatFID = patientId,
            ClaInsPayFID = payerId,
            ClaInsSequence = sequence,
            ClaInsGroupNumber = groupNumber,
            ClaInsPlanName = planName,
            ClaInsIDNumber = insuredId,
            ClaInsPriorAuthorizationNumber = authNumber,
            ClaInsAddress = address,
            ClaInsCity = city,
            ClaInsState = state,
            ClaInsZip = zip,
            ClaInsPhone = phone,
            ClaInsRelationToInsured = 0,
            ClaInsSequenceDescriptionCC = sequence == 1 ? "Primary" : "Secondary",
            ClaInsCityStateZipCC = string.Empty
        };

        var cityStateZipParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(city)) cityStateZipParts.Add(city);
        if (!string.IsNullOrWhiteSpace(state)) cityStateZipParts.Add(state);
        if (!string.IsNullOrWhiteSpace(zip)) cityStateZipParts.Add(zip);
        claimInsured.ClaInsCityStateZipCC = string.Join(", ", cityStateZipParts);

        await _db.Claim_Insureds.AddAsync(claimInsured);
        var insuredWritten = await _db.SaveChangesAsync();
        _logger.LogInformation(
            "CreateClaimInsuredRecords: Claim_Insured SaveChanges wrote {Written} rows. claimId={ClaimId}, sequence={Sequence}, payerId={PayerId}",
            insuredWritten,
            claimId,
            sequence,
            payerId);
    }

    /// <summary>
    /// Updates claim totals after all service lines are created
    /// Recalculates ClaTotalChargeTRIG and ClaTotalBalanceCC
    /// </summary>
    /// <summary>HL7 XCN in PV1: NPI (or ID) ^ Family name ^ Given name ^ ...</summary>
    private static (string? Npi, string? LastName, string? FirstName) ParsePv1Xcn(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return (null, null, null);

        var parts = field.Split('^', StringSplitOptions.None);
        string? Comp(int i) =>
            i < parts.Length && !string.IsNullOrWhiteSpace(parts[i]) ? parts[i].Trim() : null;
        return (Comp(0), Comp(1), Comp(2));
    }

    private static string? GetPhysicianFromPv1(string? pv1)
    {
        if (string.IsNullOrWhiteSpace(pv1))
            return null;

        var fields = pv1.Split('|', StringSplitOptions.None);
        var candidates = new[]
        {
            fields.Length > 7 ? fields[7] : null,
            fields.Length > 8 ? fields[8] : null,
            fields.Length > 9 ? fields[9] : null
        };

        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    private static (string? id, string? last, string? first) ParseXcn(string? xcn)
    {
        if (string.IsNullOrWhiteSpace(xcn))
            return (null, null, null);

        var parts = xcn.Split('^', StringSplitOptions.None);
        return (
            parts.Length > 0 ? parts[0] : null,
            parts.Length > 1 ? parts[1] : null,
            parts.Length > 2 ? parts[2] : null
        );
    }

    /// <summary>
    /// Resolves a physician by NPI when present, else by normalized first/last name; otherwise inserts with a generated key.
    /// </summary>
    private async Task<Physician> FindOrCreatePhysicianFromHl7Pv1Async(
        string? npi,
        string? firstName,
        string? lastName,
        string roleLabel,
        int tenantId,
        int facilityId)
    {
        var providerId = NormalizeProviderId(npi);
        string? npiNorm = null;
        if (!string.IsNullOrWhiteSpace(npi))
        {
            npiNorm = npi.Trim();
            if (npiNorm.Length > 20)
                npiNorm = npiNorm.Substring(0, 20);
        }

        var normFirst = string.IsNullOrWhiteSpace(firstName)
            ? null
            : _parser.NormalizeString(firstName, maxLength: 35);
        var normLast = string.IsNullOrWhiteSpace(lastName)
            ? null
            : _parser.NormalizeString(lastName, maxLength: 60);

        Physician? physician = null;
        if (providerId != null)
        {
            physician = await _db.Physicians
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.ExternalProviderId == providerId &&
                    p.TenantId == tenantId &&
                    p.FacilityId == facilityId);
        }
        else if (npiNorm != null)
        {
            physician = await _db.Physicians.FirstOrDefaultAsync(p =>
                p.PhyNPI == npiNorm &&
                p.TenantId == tenantId &&
                p.FacilityId == facilityId);
        }
        else if (normFirst != null || normLast != null)
        {
            physician = await _db.Physicians.FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.FacilityId == facilityId &&
                (normFirst == null
                    ? p.PhyFirstName == null || p.PhyFirstName == ""
                    : p.PhyFirstName == normFirst) &&
                (normLast == null
                    ? p.PhyLastName == null || p.PhyLastName == ""
                    : p.PhyLastName == normLast));
        }

        if (physician != null)
        {
            if (string.IsNullOrWhiteSpace(physician.ExternalProviderId) && providerId != null)
                physician.ExternalProviderId = providerId;

            if (string.IsNullOrWhiteSpace(physician.PhyFirstName) && normFirst != null)
                physician.PhyFirstName = normFirst;

            if (string.IsNullOrWhiteSpace(physician.PhyLastName) && normLast != null)
                physician.PhyLastName = normLast;

            if (string.IsNullOrWhiteSpace(physician.PhyName))
                physician.PhyName = $"{normFirst} {normLast}".Trim();

            await _db.SaveChangesAsync();

            return physician;
        }

        if (npiNorm == null && normFirst == null && normLast == null)
        {
            npiNorm = $"HL7_{Guid.NewGuid():N}"[..20];
        }

        if (normFirst == null && normLast == null)
        {
            normFirst = npiNorm;
            normLast = "HL7";
            Console.WriteLine("HL7 warning: missing data - provider name missing, applied EZClaim fallback");
        }

        if (npiNorm == null)
        {
            npiNorm = $"HL7_{Guid.NewGuid():N}"[..20];
        }

        var phyName = $"{normFirst} {normLast}".Trim();
        if (string.IsNullOrWhiteSpace(phyName))
            phyName = $"{npiNorm} HL7".Trim();
        phyName = _parser.NormalizeString(phyName, maxLength: 100) ?? phyName;
        Console.WriteLine($"[HL7 IMPORT] Physician={normFirst} {normLast}");

        physician = new Physician
        {
            PhyNPI = npiNorm,
            ExternalProviderId = providerId,
            PhyFirstName = normFirst,
            PhyLastName = normLast,
            PhyName = phyName,
            TenantId = tenantId,
            FacilityId = facilityId,
            PhyType = "Rendering",
            PhyInactive = false,
            PhyDateTimeCreated = DateTime.UtcNow
        };

        await _db.Physicians.AddAsync(physician);
        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "HL7 DFT Import → Created Physician ({Role}) PhyNPI={PhyNpi} PhyID={PhyID}",
            roleLabel,
            physician.PhyNPI ?? "(null)",
            physician.PhyID);

        return physician;
    }

    private static string? NormalizeProviderId(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        var normalized = providerId.Trim().ToUpperInvariant();
        return normalized.Length > 80 ? normalized[..80] : normalized;
    }

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
            var beforeBalance = claim.ClaTotalBalanceCC;
            claim.ClaTotalChargeTRIG = totalCharge;
            // Balance = Charges - Payments - Adjustments (simplified for import)
            claim.ClaTotalBalanceCC = totalCharge - (claim.ClaTotalAmtPaidCC ?? 0) - (claim.ClaTotalAmtAppliedCC ?? 0);
            // ClaDateTimeModified set by global audit on SaveChanges
            var written = await _db.SaveChangesAsync();
            _logger.LogInformation("UpdateClaimTotals: SaveChanges wrote {Written} rows. claimId={ClaimId}, totalCharge={TotalCharge}, beforeBalance={BeforeBalance}, afterBalance={AfterBalance}",
                written, claimId, totalCharge, beforeBalance, claim.ClaTotalBalanceCC);
        }
    }
}
