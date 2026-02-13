using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Persistence.Context;

public partial class ZeblDbContext : DbContext
{
    private readonly ICurrentUserContext? _userContext;

    public ZeblDbContext(DbContextOptions<ZeblDbContext> options)
        : base(options)
    {
    }

    public ZeblDbContext(DbContextOptions<ZeblDbContext> options, ICurrentUserContext userContext)
        : base(options)
    {
        _userContext = userContext;
    }

    public virtual DbSet<Adjustment> Adjustments { get; set; }

    public virtual DbSet<Claim> Claims { get; set; }

    public virtual DbSet<Claim_Insured> Claim_Insureds { get; set; }

    public virtual DbSet<Disbursement> Disbursements { get; set; }

    public virtual DbSet<Insured> Insureds { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<Patient_Insured> Patient_Insureds { get; set; }

    public virtual DbSet<Payer> Payers { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Physician> Physicians { get; set; }

    public virtual DbSet<Procedure_Code> Procedure_Codes { get; set; }

    public virtual DbSet<Service_Line> Service_Lines { get; set; }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<Hl7_Import_Log> Hl7_Import_Logs { get; set; }

    public virtual DbSet<Interface_Import_Log> Interface_Import_Logs { get; set; }

    public virtual DbSet<Claim_Audit> Claim_Audits { get; set; }

    public virtual DbSet<ListValue> ListValues { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Adjustment>(entity =>
        {
            entity.HasKey(e => e.AdjID).HasName("PK__Adjustme__A065A852DF7CF3F7");

            entity.ToTable("Adjustment");

            entity.Property(e => e.Adj835Ref)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AdjAmount).HasColumnType("money");
            entity.Property(e => e.AdjBatchOperationReference)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AdjCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AdjCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AdjGroupCode)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.AdjLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AdjLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AdjNote)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AdjOtherReference1)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.AdjReasonAmount).HasColumnType("money");
            entity.Property(e => e.AdjReasonCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.AdjRemarkCode)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.AdjPayF).WithMany(p => p.Adjustments)
                .HasForeignKey(d => d.AdjPayFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Adjustment_Payer");

            entity.HasOne(d => d.AdjPmtF).WithMany(p => p.Adjustments)
                .HasForeignKey(d => d.AdjPmtFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Adjustment_Payment");

            entity.HasOne(d => d.AdjSrvF).WithMany(p => p.AdjustmentAdjSrvFs)
                .HasForeignKey(d => d.AdjSrvFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Adjustment_ServiceLine_SrvID");

            entity.HasOne(d => d.AdjSrv).WithMany(p => p.AdjustmentAdjSrvs)
                .HasPrincipalKey(p => p.SrvGUID)
                .HasForeignKey(d => d.AdjSrvGUID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Adjustment_ServiceLine_SrvGUID");

            entity.HasOne(d => d.AdjTaskF).WithMany(p => p.AdjustmentAdjTaskFs)
                .HasForeignKey(d => d.AdjTaskFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Adjustment_TaskSrv");
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(e => e.ClaID).HasName("PK__Claim__E8193A9B6764AAF8");

            entity.ToTable("Claim");

            entity.Property(e => e.ClaAdmissionHour)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.ClaAdmissionSource)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaAdmissionType)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaAdmittingDiagnosis)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaBox10dClaimCodes)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaBox11bOtherClaimIDQualifier)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaBox22CodeOverride)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaBox33bOverride)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.ClaCLIANumber)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.ClaCMNCertOnFile)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaCMNCertTypeCode)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaCMNFormIdentificationCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaCN1Segment)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ClaClassification)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaConditionCode1)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaConditionCode2)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaConditionCode3)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaConditionCode4)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ClaCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaCustomField1)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ClaCustomField2)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ClaCustomField3)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ClaCustomField4)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ClaCustomField5)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ClaDMEFormData).IsUnicode(false);
            entity.Property(e => e.ClaDelayCode)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis1)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis10)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis11)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis12)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis13)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis14)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis15)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis16)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis17)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis18)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis19)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis2)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis20)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis21)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis22)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis23)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis24)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis25)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis3)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis4)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis5)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis6)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis7)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis8)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosis9)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaDiagnosisCodesCC)
                .HasMaxLength(774)
                .IsUnicode(false)
                .HasComputedColumnSql("((((((((((((((((((((((((((((((((((((((((((((((((isnull([ClaDiagnosis1],'')+'|')+isnull([ClaDiagnosis2],''))+'|')+isnull([ClaDiagnosis3],''))+'|')+isnull([ClaDiagnosis4],''))+'|')+isnull([ClaDiagnosis5],''))+'|')+isnull([ClaDiagnosis6],''))+'|')+isnull([ClaDiagnosis7],''))+'|')+isnull([ClaDiagnosis8],''))+'|')+isnull([ClaDiagnosis9],''))+'|')+isnull([ClaDiagnosis10],''))+'|')+isnull([ClaDiagnosis11],''))+'|')+isnull([ClaDiagnosis12],''))+'|')+isnull([ClaDiagnosis13],''))+'|')+isnull([ClaDiagnosis14],''))+'|')+isnull([ClaDiagnosis15],''))+'|')+isnull([ClaDiagnosis16],''))+'|')+isnull([ClaDiagnosis17],''))+'|')+isnull([ClaDiagnosis18],''))+'|')+isnull([ClaDiagnosis19],''))+'|')+isnull([ClaDiagnosis20],''))+'|')+isnull([ClaDiagnosis21],''))+'|')+isnull([ClaDiagnosis22],''))+'|')+isnull([ClaDiagnosis23],''))+'|')+isnull([ClaDiagnosis24],''))+'|')+isnull([ClaDiagnosis25],''))", true);
            entity.Property(e => e.ClaEDINotes)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.ClaEPSDTReferral)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaExternalFID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaHomeboundInd)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaHospiceInd)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaICDIndicator)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaIDENumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsuranceTypeCodeOverride)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ClaInvoiceNumber)
                .HasMaxLength(38)
                .IsUnicode(false);
            entity.Property(e => e.ClaK3FileInformation)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.ClaLabCharges)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ClaLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaMOASegment)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaMammographyCert)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ClaMedicaidResubmissionCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaMedicalRecordNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaOriginalRefNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaPOAIndicator)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.ClaPPSCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaPaperWorkControlNumber)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.ClaPaperWorkInd)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaPaperWorkTransmissionCode)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaPatientReasonDiagnosis1)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaPatientReasonDiagnosis2)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaPatientReasonDiagnosis3)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaPatientStatus)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaPricingExceptionCode)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ClaPrincipalProcedureCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaProviderAgreementCode)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaRecurringTimeFrame)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaReferralNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaRelatedToState)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.ClaRemarks)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.ClaReserved10)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.ClaReserved19)
                .HasMaxLength(75)
                .IsUnicode(false);
            entity.Property(e => e.ClaSpecialProgramIndicator)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaSubmissionMethod)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ClaTotalAdjCC)
                .HasComputedColumnSql("((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.ClaTotalAmtAppliedCC)
                .HasComputedColumnSql("((((((isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0)))+isnull([ClaTotalCOAdjTRIG],(0)))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.ClaTotalAmtPaidCC)
                .HasComputedColumnSql("(isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.ClaTotalBalanceCC)
                .HasComputedColumnSql("((isnull([ClaTotalChargeTRIG],(0))-(isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0))))-((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0))))", true)
                .HasColumnType("money");
            entity.Property(e => e.ClaTotalCOAdjTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalCRAdjTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalChargeTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalInsAmtPaidTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalInsBalanceTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalOAAdjTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalPIAdjTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalPRAdjTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalPatAmtPaidTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTotalPatBalanceTRIG).HasColumnType("money");
            entity.Property(e => e.ClaTypeOfBill)
                .HasMaxLength(4)
                .IsUnicode(false);

            entity.HasOne(d => d.ClaAttendingPhyF).WithMany(p => p.ClaimClaAttendingPhyFs)
                .HasForeignKey(d => d.ClaAttendingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_AttendingPhy");

            entity.HasOne(d => d.ClaBillingPhyF).WithMany(p => p.ClaimClaBillingPhyFs)
                .HasForeignKey(d => d.ClaBillingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_BillingPhy");

            entity.HasOne(d => d.ClaFacilityPhyF).WithMany(p => p.ClaimClaFacilityPhyFs)
                .HasForeignKey(d => d.ClaFacilityPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_FacilityPhy");

            entity.HasOne(d => d.ClaOperatingPhyF).WithMany(p => p.ClaimClaOperatingPhyFs)
                .HasForeignKey(d => d.ClaOperatingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_OperatingPhy");

            entity.HasOne(d => d.ClaOrderingPhyF).WithMany(p => p.ClaimClaOrderingPhyFs)
                .HasForeignKey(d => d.ClaOrderingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_OrderingPhy");

            entity.HasOne(d => d.ClaPatF).WithMany(p => p.Claims)
                .HasForeignKey(d => d.ClaPatFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_Patient");

            entity.HasOne(d => d.ClaReferringPhyF).WithMany(p => p.ClaimClaReferringPhyFs)
                .HasForeignKey(d => d.ClaReferringPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_ReferringPhy");

            entity.HasOne(d => d.ClaRenderingPhyF).WithMany(p => p.ClaimClaRenderingPhyFs)
                .HasForeignKey(d => d.ClaRenderingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_RenderingPhy");

            entity.HasOne(d => d.ClaSupervisingPhyF).WithMany(p => p.ClaimClaSupervisingPhyFs)
                .HasForeignKey(d => d.ClaSupervisingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_SupervisingPhy");
        });

        modelBuilder.Entity<Claim_Insured>(entity =>
        {
            entity.HasKey(e => e.ClaInsGUID).HasName("PK__Claim_In__BE610E8C4D3B9F38");

            entity.ToTable("Claim_Insured");

            entity.Property(e => e.ClaInsGUID).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ClaInsAdditionalRefID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsCity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsCityStateZipCC)
                .HasMaxLength(62)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([ClaInsCity],'')+', ')+isnull([ClaInsState],''))", true);
            entity.Property(e => e.ClaInsClaimFilingIndicator)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsEmployer)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsFirstName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsGroupNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsIDNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsLastName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsMI)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsPhone)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsPlanName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsPriorAuthorizationNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsSSN)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsSequenceDescriptionCC)
                .HasMaxLength(9)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [ClaInsSequence]=(1) then 'Primary' when [ClaInsSequence]=(2) then 'Secondary' else '' end)", true);
            entity.Property(e => e.ClaInsSex)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsState)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ClaInsZip)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.ClaInsClaF).WithMany(p => p.Claim_Insureds)
                .HasForeignKey(d => d.ClaInsClaFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClaimInsured_Claim");

            entity.HasOne(d => d.ClaInsPatF).WithMany(p => p.Claim_Insureds)
                .HasForeignKey(d => d.ClaInsPatFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClaimInsured_Patient");

            entity.HasOne(d => d.ClaInsPayF).WithMany(p => p.Claim_Insureds)
                .HasForeignKey(d => d.ClaInsPayFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClaimInsured_Payer");
        });

        modelBuilder.Entity<Disbursement>(entity =>
        {
            entity.HasKey(e => e.DisbID).HasName("PK__Disburse__140396A9001F6084");

            entity.ToTable("Disbursement");

            entity.Property(e => e.DisbAmount).HasColumnType("money");
            entity.Property(e => e.DisbBatchOperationReference)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DisbCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DisbCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DisbCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DisbLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DisbLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DisbNote)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.DisbPmtF).WithMany(p => p.Disbursements)
                .HasForeignKey(d => d.DisbPmtFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Disbursement_Payment");

            entity.HasOne(d => d.DisbSrvF).WithMany(p => p.Disbursements)
                .HasForeignKey(d => d.DisbSrvFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Disbursement_ServiceLine");
        });

        modelBuilder.Entity<Insured>(entity =>
        {
            entity.HasKey(e => e.InsGUID).HasName("PK__Insured__B098F0EA21F654F8");

            entity.ToTable("Insured");

            entity.Property(e => e.InsGUID).HasDefaultValueSql("(newid())");
            entity.Property(e => e.InsAdditionalRefID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsCity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsCityStateZipCC)
                .HasMaxLength(62)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([InsCity],'')+', ')+isnull([InsState],''))", true);
            entity.Property(e => e.InsClaimFilingIndicator)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.InsCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InsCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsEmployer)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsFirstName).HasMaxLength(50);
            entity.Property(e => e.InsGroupNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsIDNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InsLastName).HasMaxLength(50);
            entity.Property(e => e.InsLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsMI).HasMaxLength(5);
            entity.Property(e => e.InsPhone)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.InsPlanName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.InsSSN)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.InsSex)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.InsState)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.InsZip)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.InsPay).WithMany(p => p.Insureds)
                .HasForeignKey(d => d.InsPayID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Insured_Payer");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.PatID).HasName("PK__Patient__F0DDA98550DB60C7");

            entity.ToTable("Patient");

            entity.Property(e => e.PatAccountNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatAddress2)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatAptReminderPref)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PatBox8Reserved)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.PatBox9bReserved)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatBox9cReserved)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatCellPhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatCellSMTPHost)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatCity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatCityStateZipCC)
                .HasMaxLength(62)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([PatCity],'')+', ')+isnull([PatState],''))", true);
            entity.Property(e => e.PatClaimDefaults).HasColumnType("xml");
            entity.Property(e => e.PatClassification)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatCoPayAmount).HasColumnType("money");
            entity.Property(e => e.PatCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatCustomField1)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatCustomField2)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatCustomField3)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatCustomField4)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatCustomField5)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis1)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis10)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis11)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis12)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis2)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis3)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis4)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis5)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis6)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis7)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis8)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosis9)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatDiagnosisCodesCC)
                .HasMaxLength(371)
                .IsUnicode(false)
                .HasComputedColumnSql("((((((((((((((((((((((isnull([PatDiagnosis1],'')+'|')+isnull([PatDiagnosis2],''))+'|')+isnull([PatDiagnosis3],''))+'|')+isnull([PatDiagnosis4],''))+'|')+isnull([PatDiagnosis5],''))+'|')+isnull([PatDiagnosis6],''))+'|')+isnull([PatDiagnosis7],''))+'|')+isnull([PatDiagnosis8],''))+'|')+isnull([PatDiagnosis9],''))+'|')+isnull([PatDiagnosis10],''))+'|')+isnull([PatDiagnosis11],''))+'|')+isnull([PatDiagnosis12],''))", true);
            entity.Property(e => e.PatEmergencyContactName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatEmergencyContactPhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatEmergencyContactRelation)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatExternalFID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatFaxNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatFirstName).HasMaxLength(50);
            entity.Property(e => e.PatFirstNameTruncatedCC)
                .HasMaxLength(50)
                .HasComputedColumnSql("(left(isnull([PatFirstName],''),case when charindex(' ',isnull([PatFirstName]+' ',' '))=(0) then len(isnull([PatFirstName],'')) else charindex(' ',isnull([PatFirstName]+' ',' '))-(1) end))", true);
            entity.Property(e => e.PatFullNameCC)
                .HasMaxLength(108)
                .HasComputedColumnSql("((((isnull([PatLastName],'')+', ')+isnull([PatFirstName],''))+' ')+isnull([PatMI],''))", true);
            entity.Property(e => e.PatFullNameFMLCC)
                .HasMaxLength(107)
                .HasComputedColumnSql("((((isnull([PatFirstName],'')+' ')+isnull([PatMI],''))+' ')+isnull([PatLastName],''))", true);
            entity.Property(e => e.PatHeight)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PatHomePhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatLastName).HasMaxLength(50);
            entity.Property(e => e.PatLastNameTruncatedCC)
                .HasMaxLength(50)
                .HasComputedColumnSql("(left(isnull([PatLastName],''),case when charindex(' ',isnull([PatLastName]+' ',' '))=(0) then len(isnull([PatLastName],'')) else charindex(' ',isnull([PatLastName]+' ',' '))-(1) end))", true);
            entity.Property(e => e.PatLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatMI).HasMaxLength(5);
            entity.Property(e => e.PatMemberID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatPhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatPriEmail)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatReminderNote).IsUnicode(false);
            entity.Property(e => e.PatReminderNoteEvent)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatResourceWants).HasColumnType("xml");
            entity.Property(e => e.PatSSN)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.PatSecEmail)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatSex)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.PatSigSource)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.PatSigText)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatState)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementAddressLine1)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementAddressLine2)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementCity)
                .HasMaxLength(29)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementCityStateZipCC)
                .HasMaxLength(41)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([PatStatementCity],'')+', ')+isnull([PatStatementState],''))", true);
            entity.Property(e => e.PatStatementMessage)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementName)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementState)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PatStatementZipCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PatTotalBalanceCC)
                .HasComputedColumnSql("(isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.PatTotalBalanceIncludingUndisbursedPatPmtsCC)
                .HasComputedColumnSql("((isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0)))-isnull([PatTotalUndisbursedPaymentsTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.PatTotalInsBalanceTRIG).HasColumnType("money");
            entity.Property(e => e.PatTotalPatBalanceIncludingUndisbursedPatPmtsCC)
                .HasComputedColumnSql("(isnull([PatTotalPatBalanceTRIG],(0))-isnull([PatTotalUndisbursedPaymentsTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.PatTotalPatBalanceTRIG).HasColumnType("money");
            entity.Property(e => e.PatTotalUndisbursedPaymentsTRIG).HasColumnType("money");
            entity.Property(e => e.PatWeight)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PatWorkPhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PatZip)
                .HasMaxLength(20)
                .IsUnicode(false);

            // Ignore PatEZClaimPayConsent - column does not exist in database
            entity.Ignore(e => e.PatEZClaimPayConsent);

            entity.HasOne(d => d.PatBillingPhyF).WithMany(p => p.PatientPatBillingPhyFs)
                .HasForeignKey(d => d.PatBillingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_BillingPhysician");

            entity.HasOne(d => d.PatFacilityPhyF).WithMany(p => p.PatientPatFacilityPhyFs)
                .HasForeignKey(d => d.PatFacilityPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_FacilityPhysician");

            entity.HasOne(d => d.PatOrderingPhyF).WithMany(p => p.PatientPatOrderingPhyFs)
                .HasForeignKey(d => d.PatOrderingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_OrderingPhysician");

            entity.HasOne(d => d.PatReferringPhyF).WithMany(p => p.PatientPatReferringPhyFs)
                .HasForeignKey(d => d.PatReferringPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_ReferringPhysician");

            entity.HasOne(d => d.PatRenderingPhyF).WithMany(p => p.PatientPatRenderingPhyFs)
                .HasForeignKey(d => d.PatRenderingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_RenderingPhysician");

            entity.HasOne(d => d.PatSupervisingPhyF).WithMany(p => p.PatientPatSupervisingPhyFs)
                .HasForeignKey(d => d.PatSupervisingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Patient_SupervisingPhysician");
        });

        modelBuilder.Entity<Patient_Insured>(entity =>
        {
            entity.HasKey(e => e.PatInsGUID).HasName("PK__Patient___CA04E544A8F0AA6F");

            entity.ToTable("Patient_Insured");

            entity.Property(e => e.PatInsGUID).HasDefaultValueSql("(newid())");
            entity.Property(e => e.PatInsCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatInsCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatInsEligANSI).IsUnicode(false);
            entity.Property(e => e.PatInsEligStatus)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.PatInsEligStatusDisplayTextCC)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [PatInsEligStatus]='A' then 'Active' else [PatInsEligStatus] end)", true);
            entity.Property(e => e.PatInsLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PatInsLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PatInsSequenceDescriptionCC)
                .HasMaxLength(9)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [PatInsSequence]=(1) then 'Primary' when [PatInsSequence]=(2) then 'Secondary' else '' end)", true);

            entity.HasOne(d => d.PatInsIns).WithMany(p => p.Patient_Insureds)
                .HasForeignKey(d => d.PatInsInsGUID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientInsured_Insured");

            entity.HasOne(d => d.PatInsPatF).WithMany(p => p.Patient_Insureds)
                .HasForeignKey(d => d.PatInsPatFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientInsured_Patient");
        });

        modelBuilder.Entity<Payer>(entity =>
        {
            entity.HasKey(e => e.PayID).HasName("PK__Payer__EE8FCE2FDCFA87D9");

            entity.ToTable("Payer");

            entity.Property(e => e.PayAddr1)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayAddr2)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayBox1)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PayCity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayCityStateZipCC)
                .HasMaxLength(70)
                .IsUnicode(false)
                .HasComputedColumnSql("((((isnull([PayCity],'')+', ')+isnull([PayState],''))+' ')+isnull([PayZip],''))", true);
            entity.Property(e => e.PayClaimFilingIndicator)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.PayClaimType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayClassification)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PayCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PayCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayEligibilityPayerID)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PayEmail)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PayExternalID)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PayFaxNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PayICDIndicator)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.PayInsTypeCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PayLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PayLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PayNameWithInactiveCC)
                .HasMaxLength(61)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [PayInactive]=(1) then 'INACTIVE - '+isnull([PayName],'') else isnull([PayName],'') end)", true);
            entity.Property(e => e.PayNotes)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PayOfficeNumber)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PayPaymentMatchingKey)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PayPhoneNo)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PayState)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.PaySubmissionMethod)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PayTotalUndisbursedPaymentsTRIG).HasColumnType("money");
            entity.Property(e => e.PayWebsite).IsUnicode(false);
            entity.Property(e => e.PayZip)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PmtID).HasName("PK__Payment__1587830DB215BEEC");

            entity.ToTable("Payment");

            entity.Property(e => e.Pmt835Ref)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PmtAmount).HasColumnType("money");
            entity.Property(e => e.PmtAuthCode)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PmtBatchOperationReference)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PmtChargedPlatformFee).HasColumnType("money");
            entity.Property(e => e.PmtCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PmtCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PmtDisbursedTRIG).HasColumnType("money");
            entity.Property(e => e.PmtIssuerResponseCode)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.PmtLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PmtLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PmtMethod)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PmtNameOnCard)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PmtNote)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PmtOtherReference1)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PmtOtherReference2)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PmtRemainingCC)
                .HasComputedColumnSql("(isnull([PmtAmount],(0))-isnull([PmtDisbursedTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.PmtResponseCode)
                .HasMaxLength(2)
                .IsUnicode(false);

            entity.HasOne(d => d.PmtBFEPF).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PmtBFEPFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_BFEPhysician");

            entity.HasOne(d => d.PmtPatF).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PmtPatFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_Patient");

            entity.HasOne(d => d.PmtPayF).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PmtPayFID)
                .HasConstraintName("FK_Payment_Payer");
        });

        modelBuilder.Entity<Physician>(entity =>
        {
            entity.HasKey(e => e.PhyID).HasName("PK__Physicia__5FEDBF914E56D8C8");

            entity.ToTable("Physician");

            entity.Property(e => e.PhyAddress1)
                .HasMaxLength(55)
                .IsUnicode(false);
            entity.Property(e => e.PhyAddress2)
                .HasMaxLength(55)
                .IsUnicode(false);
            entity.Property(e => e.PhyCity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhyCityStateZipCC)
                .HasMaxLength(54)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([PhyCity],'')+', ')+isnull([PhyState],''))", true);
            entity.Property(e => e.PhyCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PhyCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhyEMail)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PhyEntityType)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.PhyFax)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PhyFirstMiddleLastNameCC)
                .HasMaxLength(122)
                .IsUnicode(false)
                .HasComputedColumnSql("((((isnull([PhyFirstName],'')+' ')+isnull([PhyMiddleName],''))+' ')+isnull([PhyLastName],''))", true);
            entity.Property(e => e.PhyFirstName)
                .HasMaxLength(35)
                .IsUnicode(false);
            entity.Property(e => e.PhyFullNameCC)
                .HasMaxLength(97)
                .IsUnicode(false)
                .HasComputedColumnSql("((isnull([PhyLastName],'')+', ')+isnull([PhyFirstName],''))", true);
            entity.Property(e => e.PhyLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PhyLastName)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.PhyLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhyMiddleName)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PhyNPI)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PhyName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PhyNameWithInactiveCC)
                .HasMaxLength(111)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [PhyInactive]=(1) then 'INACTIVE - '+isnull([PhyName],'') else isnull([PhyName],'') end)", true);
            entity.Property(e => e.PhyNotes).IsUnicode(false);
            entity.Property(e => e.PhyPrimaryCodeType)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.PhyPrimaryIDCode)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PhyRateClass)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhySpecialtyCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PhyState)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.PhySuffix)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PhyTelephone)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.PhyType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhyZip)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Procedure_Code>(entity =>
        {
            entity.HasKey(e => e.ProcID).HasName("PK__Procedur__07C21CFE69FC361F");

            entity.ToTable("Procedure_Code");

            entity.Property(e => e.ProcAdjust).HasColumnType("money");
            entity.Property(e => e.ProcAllowed).HasColumnType("money");
            entity.Property(e => e.ProcCategory)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcCharge).HasColumnType("money");
            entity.Property(e => e.ProcCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcCost).HasColumnType("money");
            entity.Property(e => e.ProcCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ProcCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcDescription)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ProcDrugUnitMeasurement)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ProcLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ProcLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcModifier1)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ProcModifier2)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ProcModifier3)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ProcModifier4)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.ProcModifiersCC)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasComputedColumnSql("(((isnull([ProcModifier1],'')+isnull([ProcModifier2],''))+isnull([ProcModifier3],''))+isnull([ProcModifier4],''))", true);
            entity.Property(e => e.ProcNDCCode)
                .HasMaxLength(48)
                .IsUnicode(false);
            entity.Property(e => e.ProcNote)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ProcProductCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcRateClass)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcRevenueCode)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.ProcSubCategory)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.ProcBillingPhyF).WithMany(p => p.Procedure_Codes)
                .HasForeignKey(d => d.ProcBillingPhyFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProcedureCode_BillingPhysician");

            entity.HasOne(d => d.ProcPayF).WithMany(p => p.Procedure_Codes)
                .HasForeignKey(d => d.ProcPayFID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProcedureCode_Payer");
        });

        modelBuilder.Entity<Service_Line>(entity =>
        {
            entity.HasKey(e => e.SrvID).HasName("PK__Service___0367D191FA398414");

            entity.ToTable("Service_Line");

            entity.HasIndex(e => e.SrvGUID, "UQ_ServiceLine_SrvGUID").IsUnique();

            entity.Property(e => e.SrvAdditionalData).HasColumnType("xml");
            entity.Property(e => e.SrvAllowedAmt).HasColumnType("money");
            entity.Property(e => e.SrvApprovedAmt).HasColumnType("money");
            entity.Property(e => e.SrvAuthorizationOverride)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SrvCharges).HasColumnType("money");
            entity.Property(e => e.SrvCoPayAmountDue).HasColumnType("money");
            entity.Property(e => e.SrvCost).HasColumnType("money");
            entity.Property(e => e.SrvCreatedComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.SrvCreatedUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SrvCustomField1)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvCustomField2)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvCustomField3)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvCustomField4)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvCustomField5)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvDesc)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SrvDiagnosisPointer)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.SrvDrugUnitMeasurement)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.SrvDrugUnitPrice).HasColumnType("money");
            entity.Property(e => e.SrvEMG)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.SrvEPSDT)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.SrvExpectedPriPmt).HasColumnType("money");
            entity.Property(e => e.SrvGUID).HasDefaultValueSql("(newid())");
            entity.Property(e => e.SrvK3FileInformation)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.SrvLastComputerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.SrvLastUserName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SrvModifier1)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.SrvModifier2)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.SrvModifier3)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.SrvModifier4)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.SrvModifiersCC)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasComputedColumnSql("(((isnull([SrvModifier1],'')+isnull([SrvModifier2],''))+isnull([SrvModifier3],''))+isnull([SrvModifier4],''))", true);
            entity.Property(e => e.SrvNOCOverride)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.SrvNationalDrugCode)
                .HasMaxLength(48)
                .IsUnicode(false);
            entity.Property(e => e.SrvNonCoveredCharges).HasColumnType("money");
            entity.Property(e => e.SrvPatBalanceReasonCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SrvPerUnitChargesCC).HasComputedColumnSql("(case when [SrvUnits]>(0) then [SrvCharges]/[SrvUnits] else (0) end)", true);
            entity.Property(e => e.SrvPlace)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.SrvPrescriptionNumber)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.SrvProcedureCode)
                .HasMaxLength(48)
                .IsUnicode(false);
            entity.Property(e => e.SrvProductCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SrvRespDaysAgedCC).HasComputedColumnSql("(datediff(day,[SrvRespChangeDate],sysutcdatetime()))", false);
            entity.Property(e => e.SrvRevenueCode)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.SrvTotalAdjCC)
                .HasComputedColumnSql("((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.SrvTotalAmtAppliedCC)
                .HasComputedColumnSql("((((((isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0)))+isnull([SrvTotalCOAdjTRIG],(0)))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.SrvTotalAmtPaidCC)
                .HasComputedColumnSql("(isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.SrvTotalBalanceCC)
                .HasComputedColumnSql("((isnull([SrvCharges],(0))-(isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0))))-((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0))))", true)
                .HasColumnType("money");
            entity.Property(e => e.SrvTotalCOAdjTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalCRAdjTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalInsAmtPaidTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalInsBalanceCC).HasColumnType("money");
            entity.Property(e => e.SrvTotalMinutesCC).HasComputedColumnSql("(case when [SrvStartTime] IS NOT NULL AND [SrvEndTime] IS NOT NULL then datediff(minute,[SrvStartTime],[SrvEndTime]) else (0) end)", true);
            entity.Property(e => e.SrvTotalOAAdjTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalOtherAdjCC)
                .HasComputedColumnSql("(((isnull([SrvTotalCRAdjTRIG],(0))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", true)
                .HasColumnType("money");
            entity.Property(e => e.SrvTotalPIAdjTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalPRAdjTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalPatAmtPaidTRIG).HasColumnType("money");
            entity.Property(e => e.SrvTotalPatBalanceCC).HasColumnType("money");

            entity.HasOne(d => d.SrvClaF).WithMany(p => p.Service_Lines)
                .HasForeignKey(d => d.SrvClaFID)
                .HasConstraintName("FK_ServiceLine_Claim");

            entity.HasOne(d => d.SrvResponsiblePartyNavigation).WithMany(p => p.Service_Lines)
                .HasForeignKey(d => d.SrvResponsibleParty)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceLine_ResponsibleParty");
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.UserGuid).HasName("PK__AppUser__81B7740C5F82BC9D");
            entity.ToTable("AppUser");
            entity.Property(e => e.UserGuid)
                .ValueGeneratedNever()
                .HasColumnName("UserGUID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(64);
            entity.Property(e => e.PasswordSalt).HasMaxLength(32);
            entity.Property(e => e.UserName).HasMaxLength(100);
        });

        modelBuilder.Entity<Hl7_Import_Log>(entity =>
        {
            entity.HasKey(e => e.ImportLogID).HasName("PK__Hl7_Impo__ImportLogID");
            entity.ToTable("Hl7_Import_Log");
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ImportDateTime).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ImportedBy).HasMaxLength(100);
            entity.Property(e => e.ComputerName).HasMaxLength(100);
        });

        modelBuilder.Entity<Interface_Import_Log>(entity =>
        {
            entity.HasKey(e => e.ImportID).HasName("PK_Interface_Import_Log");
            entity.ToTable("Interface_Import_Log");
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ImportDate).IsRequired();
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.ComputerName).HasMaxLength(100);
            entity.Property(e => e.TotalAmount).HasColumnType("money");
            entity.Property(e => e.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<Claim_Audit>(entity =>
        {
            entity.HasKey(e => e.AuditID).HasName("PK_Claim_Audit");
            entity.ToTable("Claim_Audit");
            entity.Property(e => e.ActivityType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ActivityDate).IsRequired();
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.ComputerName).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.TotalCharge).HasColumnType("money");
            entity.Property(e => e.InsuranceBalance).HasColumnType("money");
            entity.Property(e => e.PatientBalance).HasColumnType("money");
        });

        ConfigureListValue(modelBuilder);

        OnModelCreatingPartial(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyAudit();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAudit()
    {
        if (_userContext == null)
            return;

        var now = DateTime.UtcNow;
        var userId = _userContext.UserId;
        var userName = _userContext.UserName;
        
        // Ensure ComputerName is NEVER null or empty (fallback to server machine name).
        // This guarantees audit fields are always populated globally for all inserts/updates.
        var computerName = _userContext.ComputerName;
        if (string.IsNullOrWhiteSpace(computerName))
        {
            computerName = Environment.MachineName;
            if (string.IsNullOrWhiteSpace(computerName))
                computerName = "SERVER";
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    // On Add: Set CreatedComputerName if NULL, always set LastComputerName.
                    auditable.SetCreated(userId, userName, computerName, now);
                }
                else if (entry.State == EntityState.Modified)
                {
                    // On Update: Always set LastComputerName.
                    auditable.SetModified(userId, userName, computerName, now);
                }
            }
        }
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    
    private void ConfigureListValue(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ListValue>(entity =>
        {
            entity.HasKey(e => e.ListValueID).HasName("PK__ListValue__ListValueID");
            entity.ToTable("ListValue");
            entity.Property(e => e.ListType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Value).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
        });
    }
}
