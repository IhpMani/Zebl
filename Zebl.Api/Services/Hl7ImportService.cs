using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public Hl7ImportService(
        ZeblDbContext db,
        ILogger<Hl7ImportService> logger,
        Hl7ParserService parser)
    {
        _db = db;
        _logger = logger;
        _parser = parser;
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
        public int NewServiceLinesCount { get; set; }
    }

    /// <summary>
    /// Processes a list of HL7 DFT messages and creates Patients, Claims, and Service_Lines
    /// Each message is processed in its own transaction - if one fails, others continue
    /// </summary>
    public async Task<Hl7ImportResult> ProcessHl7Messages(List<Hl7DftMessage> messages)
    {
        var result = new Hl7ImportResult();

        foreach (var message in messages)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.PidSegment))
            {
                _logger.LogWarning("Skipping invalid HL7 message: missing PID segment");
                continue;
            }

            // Process each message in its own transaction
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var messageStats = await ProcessDftMessage(message);
                await transaction.CommitAsync();
                
                result.SuccessCount++;
                result.NewPatientsCount += messageStats.NewPatient ? 1 : 0;
                result.UpdatedPatientsCount += messageStats.NewPatient ? 0 : 1;
                result.NewClaimsCount += messageStats.NewClaim ? 1 : 0;
                result.NewServiceLinesCount += messageStats.ServiceLinesCreated;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error rolling back transaction for HL7 message");
                }

                _logger.LogError(ex, "Error processing HL7 DFT message. Continuing with next message.");
                // Continue with next message
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
    }

    /// <summary>
    /// Processes a single DFT message: Patient → Claim → Service_Lines
    /// Returns statistics about what was created
    /// </summary>
    private async Task<MessageStats> ProcessDftMessage(Hl7DftMessage message)
    {
        var stats = new MessageStats();

        // Step 1: Extract patient MRN from PID-3
        var mrn = _parser.ExtractPatientMrn(message.PidSegment);
        if (string.IsNullOrWhiteSpace(mrn))
        {
            throw new InvalidOperationException("Cannot process DFT message: PID-3 (MRN) is missing");
        }

        // Step 2: Match or create Patient
        var (patient, isNewPatient) = await MatchOrCreatePatient(message.PidSegment, mrn);
        if (patient == null || patient.PatID <= 0)
        {
            throw new InvalidOperationException($"Failed to create or retrieve patient with MRN: {mrn}");
        }
        stats.NewPatient = isNewPatient;

        // Step 3: ALWAYS create a NEW Claim per DFT message
        var (claim, isNewClaim) = await CreateNewClaim(patient.PatID, message);
        if (claim == null || claim.ClaID <= 0)
        {
            throw new InvalidOperationException($"Failed to create claim for patient {patient.PatID}");
        }
        stats.NewClaim = isNewClaim;

        // Step 4: Create Service_Lines from FT1 segments
        int serviceLinesCreated = 0;
        foreach (var ft1Segment in message.Ft1Segments)
        {
            var created = await CreateServiceLineFromFt1(claim.ClaID, ft1Segment);
            if (created) serviceLinesCreated++;
        }
        stats.ServiceLinesCreated = serviceLinesCreated;

        // Step 5: Create Claim_Insured records from IN1 segments (primary + secondary only)
        await CreateClaimInsuredRecords(claim.ClaID, patient.PatID, message.In1Segments);

        // Step 6: Update claim totals after all service lines are created
        await UpdateClaimTotals(claim.ClaID);

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
            // Required foreign keys (NOT NULL) - default to 0
            PatBillingPhyFID = 0,
            PatFacilityPhyFID = 0,
            PatOrderingPhyFID = 0,
            PatReferringPhyFID = 0,
            PatRenderingPhyFID = 0,
            PatSupervisingPhyFID = 0,
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
    /// Creates a NEW Claim for the DFT message or returns existing if duplicate
    /// EZClaim deduplication: Patient + DOS + Visit/Account
    /// Maps PV1 segment data to claim fields
    /// Returns claim and whether it was newly created
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

        // Get first service date from FT1 segments for claim date range (DOS)
        DateOnly? firstServiceDate = null;
        if (message.Ft1Segments.Count > 0)
        {
            var firstFt1 = message.Ft1Segments[0];
            // FT1-4: Transaction Date (service date) - this is the DOS
            var transactionDateStr = _parser.GetFieldValue(firstFt1, 4);
            firstServiceDate = _parser.ParseHl7Date(transactionDateStr);
        }

        // EZClaim deduplication: Match by Patient + DOS + Visit/Account
        if (firstServiceDate.HasValue)
        {
            var existingClaim = await _db.Claims
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
            // Required foreign keys (default to 0)
            ClaAttendingPhyFID = 0,
            ClaBillingPhyFID = 0,
            ClaFacilityPhyFID = 0,
            ClaOperatingPhyFID = 0,
            ClaOrderingPhyFID = 0,
            ClaReferringPhyFID = 0,
            ClaRenderingPhyFID = 0,
            ClaSupervisingPhyFID = 0
        };

        await _db.Claims.AddAsync(newClaim);
        await _db.SaveChangesAsync(); // Save to get ClaID

        if (newClaim.ClaID <= 0)
        {
            throw new InvalidOperationException($"Failed to create claim: ClaID is {newClaim.ClaID} after SaveChanges");
        }

        _logger.LogInformation("Created new claim ClaID: {ClaID} for patient PatID: {PatID}", newClaim.ClaID, patientId);
        return (newClaim, true);
    }

    /// <summary>
    /// Creates a Service_Line from an FT1 segment or returns existing if duplicate
    /// EZClaim deduplication: Claim + CPT + DOS
    /// Maps FT1 fields to Service_Line fields
    /// Returns true if service line was created, false if duplicate
    /// </summary>
    private async Task<bool> CreateServiceLineFromFt1(int claimId, string ft1Segment)
    {
        if (claimId <= 0)
        {
            _logger.LogWarning("Cannot create service line: Invalid claim ID {ClaID}", claimId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(ft1Segment))
        {
            _logger.LogWarning("Cannot create service line: FT1 segment is null or empty");
            return false;
        }

        // FT1-4: Transaction Date → SrvFromDate (DOS)
        var transactionDateStr = _parser.GetFieldValue(ft1Segment, 4);
        var serviceDate = _parser.ParseHl7Date(transactionDateStr) ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // FT1-7: Transaction Code → SrvProcedureCode (CPT)
        var procedureCodeRaw = _parser.GetFieldValue(ft1Segment, 7);
        var procedureCode = _parser.NormalizeString(procedureCodeRaw, maxLength: 30); // Normalize CPT code

        // EZClaim deduplication: Match by Claim + CPT + DOS
        if (!string.IsNullOrWhiteSpace(procedureCode))
        {
            var existingServiceLine = await _db.Service_Lines
                .FirstOrDefaultAsync(s => 
                    s.SrvClaFID == claimId &&
                    s.SrvProcedureCode == procedureCode &&
                    s.SrvFromDate == serviceDate);

            if (existingServiceLine != null)
            {
                _logger.LogInformation("Found existing service line SrvID: {SrvID} for claim ClaID: {ClaID}, CPT: {CPT}, DOS: {DOS}", 
                    existingServiceLine.SrvID, claimId, procedureCode, serviceDate);
                return false; // Skip duplicate service line
            }
        }

        // FT1-10: Transaction Amount → SrvCharges
        var chargesStr = _parser.GetFieldValue(ft1Segment, 10);
        var charges = _parser.ParseDecimal(chargesStr);

        // FT1-11: Transaction Quantity → SrvUnits
        var unitsStr = _parser.GetFieldValue(ft1Segment, 11);
        var units = float.TryParse(unitsStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var u) ? u : (float?)null;

        // FT1-13: Transaction Description → SrvDesc
        var description = _parser.GetFieldValue(ft1Segment, 13);

        var serviceLine = new Service_Line
        {
            SrvClaFID = claimId,
            SrvFromDate = serviceDate,
            SrvToDate = serviceDate, // Default to same date if no end date
            SrvProcedureCode = procedureCode, // Already normalized
            SrvDesc = description,
            SrvCharges = charges,
            SrvUnits = units,
            SrvGUID = Guid.NewGuid(),
            // Required fields with defaults
            SrvModifiersCC = string.Empty,
            SrvResponsibleParty = 0, // Default to Patient (0)
            SrvSortTiebreaker = 0,
            SrvRespChangeDate = DateTime.UtcNow
        };

        await _db.Service_Lines.AddAsync(serviceLine);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Created service line SrvID: {SrvID} for claim ClaID: {ClaID}", serviceLine.SrvID, claimId);
        return true; // Service line was created
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
