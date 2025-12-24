using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Service_Line
{
    public int SrvID { get; set; }

    public DateTime SrvDateTimeCreated { get; set; }

    public DateTime SrvDateTimeModified { get; set; }

    public Guid? SrvCreatedUserGUID { get; set; }

    public Guid? SrvLastUserGUID { get; set; }

    public string? SrvCreatedUserName { get; set; }

    public string? SrvLastUserName { get; set; }

    public string? SrvCreatedComputerName { get; set; }

    public string? SrvLastComputerName { get; set; }

    public decimal SrvAllowedAmt { get; set; }

    public decimal SrvApprovedAmt { get; set; }

    public bool SrvAttachCMN { get; set; }

    public string? SrvAuthorizationOverride { get; set; }

    public decimal SrvCharges { get; set; }

    public int? SrvClaFID { get; set; }

    public decimal SrvCoPayAmountDue { get; set; }

    public decimal SrvCost { get; set; }

    public string? SrvCustomField1 { get; set; }

    public string? SrvCustomField2 { get; set; }

    public string? SrvCustomField3 { get; set; }

    public string? SrvCustomField4 { get; set; }

    public string? SrvCustomField5 { get; set; }

    public string? SrvDesc { get; set; }

    public string? SrvDiagnosisPointer { get; set; }

    public double? SrvDrugUnitCount { get; set; }

    public string? SrvDrugUnitMeasurement { get; set; }

    public decimal? SrvDrugUnitPrice { get; set; }

    public string? SrvEMG { get; set; }

    public DateTime? SrvEndTime { get; set; }

    public string? SrvEPSDT { get; set; }

    public decimal SrvExpectedPriPmt { get; set; }

    public DateOnly? SrvFirstInsPaymentDateTRIG { get; set; }

    public DateOnly SrvFromDate { get; set; }

    public Guid SrvGUID { get; set; }

    public string? SrvK3FileInformation { get; set; }

    public string? SrvModifier1 { get; set; }

    public string? SrvModifier2 { get; set; }

    public string? SrvModifier3 { get; set; }

    public string? SrvModifier4 { get; set; }

    public string? SrvNationalDrugCode { get; set; }

    public decimal SrvNonCoveredCharges { get; set; }

    public string? SrvPatBalanceReasonCode { get; set; }

    public string? SrvPlace { get; set; }

    public string? SrvPrescriptionNumber { get; set; }

    public bool SrvPrintLineItem { get; set; }

    public string? SrvProcedureCode { get; set; }

    public string? SrvProductCode { get; set; }

    public DateTime SrvRespChangeDate { get; set; }

    public int SrvResponsibleParty { get; set; }

    public string? SrvRevenueCode { get; set; }

    public int SrvSortTiebreaker { get; set; }

    public DateTime? SrvStartTime { get; set; }

    public DateOnly SrvToDate { get; set; }

    public decimal SrvTotalCOAdjTRIG { get; set; }

    public decimal SrvTotalCRAdjTRIG { get; set; }

    public decimal SrvTotalOAAdjTRIG { get; set; }

    public decimal SrvTotalPIAdjTRIG { get; set; }

    public decimal SrvTotalPRAdjTRIG { get; set; }

    public decimal SrvTotalInsAmtPaidTRIG { get; set; }

    public decimal SrvTotalPatAmtPaidTRIG { get; set; }

    public float? SrvUnits { get; set; }

    public float? SrvPerUnitChargesCC { get; set; }

    public string SrvModifiersCC { get; set; } = null!;

    public int? SrvRespDaysAgedCC { get; set; }

    public decimal? SrvTotalAdjCC { get; set; }

    public decimal? SrvTotalOtherAdjCC { get; set; }

    public decimal? SrvTotalAmtAppliedCC { get; set; }

    public decimal? SrvTotalAmtPaidCC { get; set; }

    public decimal? SrvTotalBalanceCC { get; set; }

    public decimal? SrvTotalInsBalanceCC { get; set; }

    public decimal? SrvTotalPatBalanceCC { get; set; }

    public int? SrvTotalMinutesCC { get; set; }

    public string? SrvAdditionalData { get; set; }

    public string? SrvNOCOverride { get; set; }

    public virtual ICollection<Adjustment> AdjustmentAdjSrvFs { get; set; } = new List<Adjustment>();

    public virtual ICollection<Adjustment> AdjustmentAdjSrvs { get; set; } = new List<Adjustment>();

    public virtual ICollection<Adjustment> AdjustmentAdjTaskFs { get; set; } = new List<Adjustment>();

    public virtual ICollection<Disbursement> Disbursements { get; set; } = new List<Disbursement>();

    public virtual Claim? SrvClaF { get; set; }

    public virtual Payer SrvResponsiblePartyNavigation { get; set; } = null!;
}
