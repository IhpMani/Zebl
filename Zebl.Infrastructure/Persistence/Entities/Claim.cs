using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Claim
{
    public int ClaID { get; set; }

    public DateTime ClaDateTimeCreated { get; set; }

    public DateTime ClaDateTimeModified { get; set; }

    public Guid? ClaCreatedUserGUID { get; set; }

    public Guid? ClaLastUserGUID { get; set; }

    public string? ClaCreatedUserName { get; set; }

    public string? ClaLastUserName { get; set; }

    public string? ClaCreatedComputerName { get; set; }

    public string? ClaLastComputerName { get; set; }

    public DateOnly? ClaAccidentDate { get; set; }

    public DateOnly? ClaAcuteManifestationDate { get; set; }

    public string? ClaAdmissionHour { get; set; }

    public string? ClaAdmissionSource { get; set; }

    public string? ClaAdmissionType { get; set; }

    public DateOnly? ClaAdmittedDate { get; set; }

    public string? ClaAdmittingDiagnosis { get; set; }

    public bool? ClaArchived { get; set; }

    public DateOnly? ClaAssumedCareDate { get; set; }

    public int ClaAttendingPhyFID { get; set; }

    public DateOnly? ClaAuthorizedReturnToWorkDate { get; set; }

    public DateOnly? ClaBillDate { get; set; }

    public int ClaBillingPhyFID { get; set; }

    public int? ClaBillTo { get; set; }

    public string? ClaBox10dClaimCodes { get; set; }

    public string? ClaBox11bOtherClaimIDQualifier { get; set; }

    public string? ClaBox22CodeOverride { get; set; }

    public string? ClaBox33bOverride { get; set; }

    public string? ClaClassification { get; set; }

    public string? ClaCLIANumber { get; set; }

    public string? ClaCMNCertOnFile { get; set; }

    public string? ClaCMNCertTypeCode { get; set; }

    public string? ClaCMNFormIdentificationCode { get; set; }

    public DateOnly? ClaCMNInitialDate { get; set; }

    public int? ClaCMNLengthOfNeed { get; set; }

    public DateOnly? ClaCMNRevisedDate { get; set; }

    public DateOnly? ClaCMNSignedDate { get; set; }

    public string? ClaCN1Segment { get; set; }

    public string? ClaConditionCode1 { get; set; }

    public string? ClaConditionCode2 { get; set; }

    public string? ClaConditionCode3 { get; set; }

    public string? ClaConditionCode4 { get; set; }

    public string? ClaCustomField1 { get; set; }

    public string? ClaCustomField2 { get; set; }

    public string? ClaCustomField3 { get; set; }

    public string? ClaCustomField4 { get; set; }

    public string? ClaCustomField5 { get; set; }

    public DateOnly? ClaDateLastSeen { get; set; }

    public DateOnly? ClaDateOfCurrent { get; set; }

    public DateOnly? ClaDateTotalFrom { get; set; }

    public DateOnly? ClaDateTotalThrough { get; set; }

    public string? ClaDelayCode { get; set; }

    public string? ClaDiagnosis1 { get; set; }

    public string? ClaDiagnosis2 { get; set; }

    public string? ClaDiagnosis3 { get; set; }

    public string? ClaDiagnosis4 { get; set; }

    public string? ClaDiagnosis5 { get; set; }

    public string? ClaDiagnosis6 { get; set; }

    public string? ClaDiagnosis7 { get; set; }

    public string? ClaDiagnosis8 { get; set; }

    public string? ClaDiagnosis9 { get; set; }

    public string? ClaDiagnosis10 { get; set; }

    public string? ClaDiagnosis11 { get; set; }

    public string? ClaDiagnosis12 { get; set; }

    public string? ClaDiagnosis13 { get; set; }

    public string? ClaDiagnosis14 { get; set; }

    public string? ClaDiagnosis15 { get; set; }

    public string? ClaDiagnosis16 { get; set; }

    public string? ClaDiagnosis17 { get; set; }

    public string? ClaDiagnosis18 { get; set; }

    public string? ClaDiagnosis19 { get; set; }

    public string? ClaDiagnosis20 { get; set; }

    public string? ClaDiagnosis21 { get; set; }

    public string? ClaDiagnosis22 { get; set; }

    public string? ClaDiagnosis23 { get; set; }

    public string? ClaDiagnosis24 { get; set; }

    public string? ClaDiagnosis25 { get; set; }

    public string ClaDiagnosisCodesCC { get; set; } = null!;

    public DateOnly? ClaDisabilityBeginDate { get; set; }

    public DateOnly? ClaDisabilityEndDate { get; set; }

    public DateOnly? ClaDischargedDate { get; set; }

    public DateTime? ClaDischargedHour { get; set; }

    public string? ClaDMEFormData { get; set; }

    public string? ClaEDINotes { get; set; }

    public string? ClaEPSDTReferral { get; set; }

    public string? ClaExternalFID { get; set; }

    public int ClaFacilityPhyFID { get; set; }

    public DateOnly? ClaFirstDateTRIG { get; set; }

    public DateOnly? ClaFirstDateOfInjury { get; set; }

    public DateOnly? ClaFirstInsPaymentDateTRIG { get; set; }

    public DateOnly? ClaHearingAndPrescriptionDate { get; set; }

    public string? ClaHomeboundInd { get; set; }

    public string? ClaHospiceInd { get; set; }

    public string ClaICDIndicator { get; set; } = null!;

    public string? ClaIDENumber { get; set; }

    public DateOnly? ClaInitialTreatmentDate { get; set; }

    public short ClaIgnoreAppliedAmount { get; set; }

    public string? ClaInsuranceTypeCodeOverride { get; set; }

    public string? ClaInvoiceNumber { get; set; }

    public string? ClaK3FileInformation { get; set; }

    public string? ClaLabCharges { get; set; }

    public DateOnly? ClaLastDateTRIG { get; set; }

    public DateOnly? ClaLastExportedDate { get; set; }

    public DateOnly? ClaLastMenstrualDate { get; set; }

    public DateOnly? ClaLastPrintedDate { get; set; }

    public DateOnly? ClaLastWorkedDate { get; set; }

    public DateOnly? ClaLastXRayDate { get; set; }

    public bool ClaLocked { get; set; }

    public string? ClaMammographyCert { get; set; }

    public string? ClaMedicalRecordNumber { get; set; }

    public string? ClaMedicaidResubmissionCode { get; set; }

    public string? ClaMOASegment { get; set; }

    public int ClaOperatingPhyFID { get; set; }

    public int ClaOrderingPhyFID { get; set; }

    public string? ClaOriginalRefNo { get; set; }

    public int? ClaOutsideLab { get; set; }

    public DateOnly? ClaPaidDateTRIG { get; set; }

    public string? ClaPaperWorkControlNumber { get; set; }

    public string? ClaPaperWorkInd { get; set; }

    public string? ClaPaperWorkTransmissionCode { get; set; }

    public int ClaPatFID { get; set; }

    public string? ClaPatientReasonDiagnosis1 { get; set; }

    public string? ClaPatientReasonDiagnosis2 { get; set; }

    public string? ClaPatientReasonDiagnosis3 { get; set; }

    public string? ClaPatientStatus { get; set; }

    public string? ClaPOAIndicator { get; set; }

    public string? ClaPPSCode { get; set; }

    public string? ClaPricingExceptionCode { get; set; }

    public string? ClaPrincipalProcedureCode { get; set; }

    public DateOnly? ClaPrincipalProcedureDate { get; set; }

    public bool ClaPrintUnitCharge { get; set; }

    public string? ClaProviderAgreementCode { get; set; }

    public DateOnly? ClaRecurUntilDate { get; set; }

    public string? ClaRecurringTimeFrame { get; set; }

    public string? ClaReferralNumber { get; set; }

    public int ClaReferringPhyFID { get; set; }

    public short? ClaRelatedTo { get; set; }

    public string? ClaRelatedToState { get; set; }

    public DateOnly? ClaRelinquishedCareDate { get; set; }

    public string? ClaRemarks { get; set; }

    public int ClaRenderingPhyFID { get; set; }

    public string? ClaReserved10 { get; set; }

    public string? ClaReserved19 { get; set; }

    public DateOnly? ClaSimilarIllnessDate { get; set; }

    public string? ClaSpecialProgramIndicator { get; set; }

    public DateOnly? ClaStatementCoversFromOverride { get; set; }

    public DateOnly? ClaStatementCoversThroughOverride { get; set; }

    public string? ClaStatus { get; set; }

    public string ClaSubmissionMethod { get; set; } = null!;

    public int ClaSupervisingPhyFID { get; set; }

    public string? ClaTypeOfBill { get; set; }

    public decimal? ClaTotalCOAdjTRIG { get; set; }

    public decimal? ClaTotalCRAdjTRIG { get; set; }

    public decimal? ClaTotalOAAdjTRIG { get; set; }

    public decimal? ClaTotalPIAdjTRIG { get; set; }

    public decimal? ClaTotalPRAdjTRIG { get; set; }

    public decimal? ClaTotalAdjCC { get; set; }

    public int ClaTotalServiceLineCountTRIG { get; set; }

    public decimal ClaTotalChargeTRIG { get; set; }

    public decimal ClaTotalInsAmtPaidTRIG { get; set; }

    public decimal ClaTotalInsBalanceTRIG { get; set; }

    public decimal ClaTotalPatAmtPaidTRIG { get; set; }

    public decimal ClaTotalPatBalanceTRIG { get; set; }

    public decimal? ClaTotalAmtAppliedCC { get; set; }

    public decimal? ClaTotalAmtPaidCC { get; set; }

    public decimal? ClaTotalBalanceCC { get; set; }

    public virtual Physician ClaAttendingPhyF { get; set; } = null!;

    public virtual Physician ClaBillingPhyF { get; set; } = null!;

    public virtual Physician ClaFacilityPhyF { get; set; } = null!;

    public virtual Physician ClaOperatingPhyF { get; set; } = null!;

    public virtual Physician ClaOrderingPhyF { get; set; } = null!;

    public virtual Patient ClaPatF { get; set; } = null!;

    public virtual Physician ClaReferringPhyF { get; set; } = null!;

    public virtual Physician ClaRenderingPhyF { get; set; } = null!;

    public virtual Physician ClaSupervisingPhyF { get; set; } = null!;

    public virtual ICollection<Claim_Insured> Claim_Insureds { get; set; } = new List<Claim_Insured>();

    public virtual ICollection<Service_Line> Service_Lines { get; set; } = new List<Service_Line>();
}
