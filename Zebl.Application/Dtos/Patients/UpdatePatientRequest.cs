using System;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Patients
{
    public class UpdatePatientRequest
    {
        public string? PatFirstName { get; set; }
        public string? PatLastName { get; set; }
        public string? PatMI { get; set; }
        public string? PatAccountNo { get; set; }
        public bool? PatActive { get; set; }
        public DateTime? PatBirthDate { get; set; } // JSON sends date string; convert to DateOnly when applying
        public string? PatSSN { get; set; }
        public string? PatSex { get; set; }
        public string? PatAddress { get; set; }
        public string? PatAddress2 { get; set; }
        public string? PatCity { get; set; }
        public string? PatState { get; set; }
        public string? PatZip { get; set; }
        public string? PatPhoneNo { get; set; }
        public string? PatCellPhoneNo { get; set; }
        public string? PatHomePhoneNo { get; set; }
        public string? PatWorkPhoneNo { get; set; }
        public string? PatFaxNo { get; set; }
        public string? PatPriEmail { get; set; }
        public string? PatSecEmail { get; set; }
        public string? PatClassification { get; set; }
        public int? PatClaLibFID { get; set; }
        public decimal? PatCoPayAmount { get; set; }
        public string? PatDiagnosis1 { get; set; }
        public string? PatDiagnosis2 { get; set; }
        public string? PatDiagnosis3 { get; set; }
        public string? PatDiagnosis4 { get; set; }
        public int? PatEmployed { get; set; }
        public short? PatMarried { get; set; }
        public int? PatRenderingPhyFID { get; set; }
        public int? PatBillingPhyFID { get; set; }
        public int? PatFacilityPhyFID { get; set; }
        public int? PatReferringPhyFID { get; set; }
        public int? PatOrderingPhyFID { get; set; }
        public int? PatSupervisingPhyFID { get; set; }
        public string? PatStatementName { get; set; }
        public string? PatStatementAddressLine1 { get; set; }
        public string? PatStatementAddressLine2 { get; set; }
        public string? PatStatementCity { get; set; }
        public string? PatStatementState { get; set; }
        public string? PatStatementZipCode { get; set; }
        public string? PatStatementMessage { get; set; }
        public string? PatReminderNote { get; set; }
        public string? PatEmergencyContactName { get; set; }
        public string? PatEmergencyContactPhoneNo { get; set; }
        public string? PatEmergencyContactRelation { get; set; }
        public string? PatWeight { get; set; }
        public string? PatHeight { get; set; }
        public string? PatMemberID { get; set; }
        public bool? PatSigOnFile { get; set; }
        public bool? PatInsuredSigOnFile { get; set; }
        public bool? PatPrintSigDate { get; set; }
        public bool? PatPhyPrintDate { get; set; }
        public bool? PatDontSendPromotions { get; set; }
        public bool? PatDontSendStatements { get; set; }
        public bool? PatAuthTracking { get; set; }
        public string? PatAptReminderPref { get; set; }
        public string? PatReminderNoteEvent { get; set; }
        public string? PatSigSource { get; set; }
        public float? PatCoPayPercent { get; set; }
        public string? PatCustomField1 { get; set; }
        public string? PatCustomField2 { get; set; }
        public string? PatCustomField3 { get; set; }
        public string? PatCustomField4 { get; set; }
        public string? PatCustomField5 { get; set; }
        public string? PatExternalFID { get; set; }
        public string? PatPaymentMatchingKey { get; set; }
        public DateTime? PatLastStatementDateTRIG { get; set; }

        /// <summary>Insurance list with sequence. Primary = 1, Secondary = 2. On save, Patient_Insured updated accordingly.</summary>
        public List<InsuranceUpdateDto>? InsuranceList { get; set; }

        /// <summary>Optional note for Claim_Audit. If empty, use "Patient information updated."</summary>
        public string? NoteText { get; set; }

        /// <summary>When true, update all claims' primary insurance (ClaInsSequence=1) from patient's primary insurance (PatInsSequence=1).</summary>
        public bool? UpdateClaims { get; set; }
    }

    public class InsuranceUpdateDto
    {
        public Guid? PatInsGUID { get; set; }
        public int Sequence { get; set; }
        public int? PayID { get; set; }
        public string? GroupNumber { get; set; }
        public string? MemberID { get; set; }
        public string? InsFirstName { get; set; }
        public string? InsLastName { get; set; }
        public string? InsMI { get; set; }
        public string? PlanName { get; set; }
        public int RelationToInsured { get; set; }
        public DateTime? InsBirthDate { get; set; }
        public string? InsAddress { get; set; }
        public string? InsCity { get; set; }
        public string? InsState { get; set; }
        public string? InsZip { get; set; }
        public string? InsPhone { get; set; }
        public string? InsEmployer { get; set; }
        public short? InsAcceptAssignment { get; set; }
        public string? InsClaimFilingIndicator { get; set; }
        public string? InsSSN { get; set; }
    }
}
