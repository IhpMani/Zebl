using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Patients
{
    /// <summary>List row for Find Patient: mirrors scalar Patient columns (no navigations).</summary>
    public class PatientListItemDto
    {
        [Required] public int PatID { get; set; }
        public string? PatFirstName { get; set; }
        public string? PatLastName { get; set; }
        public string? PatMI { get; set; }
        public string? PatFullNameCC { get; set; }
        public DateTime PatDateTimeCreated { get; set; }
        public DateTime PatDateTimeModified { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public Guid? PatCreatedUserGUID { get; set; }
        public Guid? PatLastUserGUID { get; set; }
        public string? PatCreatedUserName { get; set; }
        public string? PatLastUserName { get; set; }
        public string? PatCreatedComputerName { get; set; }
        public string? PatLastComputerName { get; set; }
        public string? PatAccountNo { get; set; }
        public bool PatActive { get; set; }
        public string? PatAddress { get; set; }
        public string? PatAddress2 { get; set; }
        public string? PatAptReminderPref { get; set; }
        public bool? PatAuthTracking { get; set; }
        public int PatBillingPhyFID { get; set; }
        public DateOnly? PatBirthDate { get; set; }
        public string? PatBox8Reserved { get; set; }
        public string? PatBox9bReserved { get; set; }
        public string? PatBox9cReserved { get; set; }
        public string? PatCellPhoneNo { get; set; }
        public string? PatCellSMTPHost { get; set; }
        public string? PatCity { get; set; }
        public int PatClaLibFID { get; set; }
        public string? PatClaimDefaults { get; set; }
        public string? PatClassification { get; set; }
        public decimal? PatCoPayAmount { get; set; }
        public float? PatCoPayPercent { get; set; }
        public string? PatCustomField1 { get; set; }
        public string? PatCustomField2 { get; set; }
        public string? PatCustomField3 { get; set; }
        public string? PatCustomField4 { get; set; }
        public string? PatCustomField5 { get; set; }
        public string? PatDiagnosis1 { get; set; }
        public string? PatDiagnosis2 { get; set; }
        public string? PatDiagnosis3 { get; set; }
        public string? PatDiagnosis4 { get; set; }
        public string? PatDiagnosis5 { get; set; }
        public string? PatDiagnosis6 { get; set; }
        public string? PatDiagnosis7 { get; set; }
        public string? PatDiagnosis8 { get; set; }
        public string? PatDiagnosis9 { get; set; }
        public string? PatDiagnosis10 { get; set; }
        public string? PatDiagnosis11 { get; set; }
        public string? PatDiagnosis12 { get; set; }
        public bool PatDontSendPromotions { get; set; }
        public bool PatDontSendStatements { get; set; }
        public string? PatEmergencyContactName { get; set; }
        public string? PatEmergencyContactPhoneNo { get; set; }
        public string? PatEmergencyContactRelation { get; set; }
        public int? PatEmployed { get; set; }
        public string? PatExternalFID { get; set; }
        public string? PatPaymentMatchingKey { get; set; }
        public bool PatEZClaimPayConsent { get; set; }
        public int PatFacilityPhyFID { get; set; }
        public string? PatFaxNo { get; set; }
        public DateOnly? PatFirstDateTRIG { get; set; }
        public string? PatHeight { get; set; }
        public string? PatHomePhoneNo { get; set; }
        public bool PatInsuredSigOnFile { get; set; }
        public DateTime? PatLastAppointmentKeptTRIG { get; set; }
        public DateTime? PatLastAppointmentNotKeptTRIG { get; set; }
        public DateOnly? PatLastServiceDateTRIG { get; set; }
        public DateTime? PatLastCellSMPTHostUpdate { get; set; }
        public DateTime? PatLastStatementDateTRIG { get; set; }
        public DateOnly? PatLastPatPmtDateTRIG { get; set; }
        public bool PatLocked { get; set; }
        public short? PatMarried { get; set; }
        public string? PatMemberID { get; set; }
        public int PatOrderingPhyFID { get; set; }
        public string? PatPhoneNo { get; set; }
        public bool? PatPhyPrintDate { get; set; }
        public string? PatPriEmail { get; set; }
        public bool? PatPrintSigDate { get; set; }
        public int PatReferringPhyFID { get; set; }
        public DateOnly? PatRecallDate { get; set; }
        public string? PatReminderNote { get; set; }
        public string? PatReminderNoteEvent { get; set; }
        public int PatRenderingPhyFID { get; set; }
        public string? PatResourceWants { get; set; }
        public string? PatSecEmail { get; set; }
        public string? PatSex { get; set; }
        public bool PatSigOnFile { get; set; }
        public string? PatSigSource { get; set; }
        public string? PatSigText { get; set; }
        public string? PatSSN { get; set; }
        public string? PatState { get; set; }
        public string? PatStatementAddressLine1 { get; set; }
        public string? PatStatementAddressLine2 { get; set; }
        public string? PatStatementCity { get; set; }
        public string? PatStatementName { get; set; }
        public string? PatStatementMessage { get; set; }
        public string? PatStatementState { get; set; }
        public string? PatStatementZipCode { get; set; }
        public int PatSupervisingPhyFID { get; set; }
        public decimal PatTotalInsBalanceTRIG { get; set; }
        public decimal PatTotalPatBalanceTRIG { get; set; }
        public decimal PatTotalUndisbursedPaymentsTRIG { get; set; }
        public decimal? PatTotalBalanceCC { get; set; }
        public string? PatWeight { get; set; }
        public string? PatWorkPhoneNo { get; set; }
        public string? PatZip { get; set; }
        public DateTime? PatLastPaymentRequestTRIG { get; set; }
        public string? PatFirstNameTruncatedCC { get; set; }
        public string? PatLastNameTruncatedCC { get; set; }
        public string? PatFullNameFMLCC { get; set; }
        public string? PatDiagnosisCodesCC { get; set; }
        public decimal? PatTotalBalanceIncludingUndisbursedPatPmtsCC { get; set; }
        public decimal? PatTotalPatBalanceIncludingUndisbursedPatPmtsCC { get; set; }
        public string? PatCityStateZipCC { get; set; }
        public string? PatStatementCityStateZipCC { get; set; }
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
