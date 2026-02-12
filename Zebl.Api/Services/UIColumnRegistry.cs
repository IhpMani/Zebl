namespace Zebl.Api.Services;

/// <summary>
/// Strict whitelist of UI-exposable columns per entity.
/// ONLY columns listed here can be exposed via the Add Column feature.
/// This is the single source of truth for UI column visibility.
/// </summary>
public static class UIColumnRegistry
{
    /// <summary>
    /// Whitelist of allowed columns per entity.
    /// Key: Entity name (e.g., "Claim")
    /// Value: List of property names from EF entity
    /// </summary>
    public static readonly Dictionary<string, List<string>> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "Claim",
            new List<string>
            {
                // Identity
                "ClaID",
                
                // Status & Classification
                "ClaStatus",
                "ClaClassification",
                
                // Financial
                "ClaTotalCharge",
                "ClaTotalBalance",
                "ClaTotalInsAmtPaid",
                "ClaTotalPatAmtPaid",
                "ClaTotalInsBalance",
                "ClaTotalPatBalance",
                
                // Dates
                "ClaBillDate",
                "ClaDateOfCurrent",
                "ClaFirstDateOfInjury",
                "ClaLastWorkedDate",
                
                // Foreign Keys (Patient, Physicians, Payer)
                "PatFID",
                "AttendingPhyFID",
                "ReferringPhyFID",
                "RenderingPhyFID",
                "OperatingPhyFID",
                "BillingPhyFID",
                
                // Billing
                "ClaTypeOfBill",
                "ClaBillTo",
                "ClaSubmissionMethod",
                
                // Clinical
                "ClaDiagnosis1",
                "ClaDiagnosis2",
                "ClaDiagnosis3",
                "ClaDiagnosis4",
                "ClaICDIndicator",
                
                // Other
                "ClaMedicalRecordNumber",
                "ClaReferralNumber",
                "ClaRemarks",
                "ClaLocked",
                "ClaArchived"
            }
        },
        {
            "Patient",
            new List<string>
            {
                // Identity
                "PatID",
                "PatAccountNo",
                
                // Demographics
                "PatLastName",
                "PatFirstName",
                "PatMiddleName",
                "PatDOB",
                "PatSex",
                "PatSSN",
                
                // Contact
                "PatAddress1",
                "PatAddress2",
                "PatCity",
                "PatState",
                "PatZipCode",
                "PatHomePhone",
                "PatWorkPhone",
                "PatCellPhone",
                "PatEmail",
                
                // Classification
                "PatClassification",
                
                // Foreign Keys
                "PatFacilityID",
                
                // Other
                "PatActive",
                "PatEmployer",
                "PatEmergencyContact",
                "PatEmergencyPhone"
            }
        },
        {
            "Payment",
            new List<string>
            {
                // Identity
                "PayID",
                "PayBatchID",
                
                // Financial
                "PayAmount",
                "PayCheckNumber",
                "PayCheckDate",
                
                // Foreign Keys
                "PayPayerFID",
                "PayClaimFID",
                
                // Method & Status
                "PayMethod",
                "PayType",
                "PayStatus",
                
                // Dates
                "PayDatePosted",
                "PayDepositDate",
                
                // Other
                "PayReferenceNumber",
                "PayRemarks"
            }
        },
        {
            "Adjustment",
            new List<string>
            {
                // Identity
                "AdjID",
                
                // Financial
                "AdjAmount",
                "AdjType",
                
                // Foreign Keys
                "AdjClaimFID",
                "AdjServiceLineFID",
                
                // Dates
                "AdjDate",
                
                // Other
                "AdjReason",
                "AdjRemarks"
            }
        },
        {
            "Disbursement",
            new List<string>
            {
                // Identity
                "DisID",
                "DisBatchID",
                
                // Financial
                "DisAmount",
                "DisCheckNumber",
                
                // Foreign Keys
                "DisPhysicianFID",
                "DisClaimFID",
                
                // Dates
                "DisCheckDate",
                "DisDatePosted",
                
                // Status
                "DisStatus",
                "DisVoided",
                
                // Other
                "DisRemarks"
            }
        },
        {
            "Payer",
            new List<string>
            {
                // Identity
                "PayID",
                
                // Name & Contact
                "PayName",
                "PayAddress1",
                "PayAddress2",
                "PayCity",
                "PayState",
                "PayZipCode",
                "PayPhone",
                "PayFax",
                "PayEmail",
                
                // Classification
                "PayClassification",
                "PayType",
                
                // Billing
                "PayPayerID",
                "PayEDIPayerID",
                
                // Status
                "PayActive"
            }
        },
        {
            "Physician",
            new List<string>
            {
                // Identity
                "PhyID",
                
                // Name
                "PhyLastName",
                "PhyFirstName",
                "PhyMiddleName",
                "PhyCredentials",
                
                // Contact
                "PhyAddress1",
                "PhyAddress2",
                "PhyCity",
                "PhyState",
                "PhyZipCode",
                "PhyPhone",
                "PhyFax",
                "PhyEmail",
                
                // Classification
                "PhyType",
                "PhySpecialty",
                
                // IDs
                "PhyNPI",
                "PhySSN",
                "PhyTaxID",
                "PhyLicenseNumber",
                
                // Status
                "PhyActive"
            }
        },
        {
            "Service_Line",
            new List<string>
            {
                // Identity
                "SerID",
                "SerLineNumber",
                
                // Foreign Keys
                "SerClaimFID",
                "SerProcedureCodeFID",
                
                // Financial
                "SerChargeAmount",
                "SerUnits",
                "SerTotalCharge",
                "SerInsAmtPaid",
                "SerPatAmtPaid",
                "SerBalance",
                
                // Dates
                "SerDateOfService",
                "SerDateFrom",
                "SerDateTo",
                
                // Clinical
                "SerModifier1",
                "SerModifier2",
                "SerModifier3",
                "SerModifier4",
                "SerDiagnosisPointer",
                
                // Other
                "SerPlaceOfService",
                "SerRemarks"
            }
        },
        {
            "Procedure_Code",
            new List<string>
            {
                // Identity
                "ProcID",
                "ProcCode",
                
                // Description
                "ProcDescription",
                "ProcLongDescription",
                
                // Classification
                "ProcCategory",
                "ProcSubCategory",
                
                // Financial
                "ProcDefaultCharge",
                "ProcRateClass",
                
                // Status
                "ProcActive"
            }
        }
    };

    /// <summary>
    /// Check if an entity is supported
    /// </summary>
    public static bool IsEntitySupported(string entityName)
    {
        return AllowedColumns.ContainsKey(entityName);
    }

    /// <summary>
    /// Get allowed columns for an entity
    /// </summary>
    public static List<string> GetAllowedColumns(string entityName)
    {
        return AllowedColumns.TryGetValue(entityName, out var columns) 
            ? columns 
            : new List<string>();
    }

    /// <summary>
    /// Get all supported entity names
    /// </summary>
    public static List<string> GetSupportedEntities()
    {
        return AllowedColumns.Keys.OrderBy(k => k).ToList();
    }
}
