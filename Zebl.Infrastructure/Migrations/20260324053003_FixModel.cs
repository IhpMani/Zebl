using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_Payer",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_Payment",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvGUID",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvID",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_TaskSrv",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_AttendingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_BillingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_FacilityPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_OperatingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_OrderingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_Patient",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_ReferringPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_RenderingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_SupervisingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_BillingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_FacilityPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_OrderingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_ReferringPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_RenderingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_SupervisingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_BFEPhysician",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_Patient",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcedureCode_BillingPhysician",
                table: "Procedure_Code");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceLine_Claim",
                table: "Service_Line");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_Payer",
                table: "Adjustment",
                column: "AdjPayFID",
                principalTable: "Payer",
                principalColumn: "PayID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_Payment",
                table: "Adjustment",
                column: "AdjPmtFID",
                principalTable: "Payment",
                principalColumn: "PmtID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvGUID",
                table: "Adjustment",
                column: "AdjSrvGUID",
                principalTable: "Service_Line",
                principalColumn: "SrvGUID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvID",
                table: "Adjustment",
                column: "AdjSrvFID",
                principalTable: "Service_Line",
                principalColumn: "SrvID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_TaskSrv",
                table: "Adjustment",
                column: "AdjTaskFID",
                principalTable: "Service_Line",
                principalColumn: "SrvID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_AttendingPhy",
                table: "Claim",
                column: "ClaAttendingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_BillingPhy",
                table: "Claim",
                column: "ClaBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_FacilityPhy",
                table: "Claim",
                column: "ClaFacilityPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_OperatingPhy",
                table: "Claim",
                column: "ClaOperatingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_OrderingPhy",
                table: "Claim",
                column: "ClaOrderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_Patient",
                table: "Claim",
                column: "ClaPatFID",
                principalTable: "Patient",
                principalColumn: "PatID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_ReferringPhy",
                table: "Claim",
                column: "ClaReferringPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_RenderingPhy",
                table: "Claim",
                column: "ClaRenderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_SupervisingPhy",
                table: "Claim",
                column: "ClaSupervisingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_BillingPhysician",
                table: "Patient",
                column: "PatBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_FacilityPhysician",
                table: "Patient",
                column: "PatFacilityPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_OrderingPhysician",
                table: "Patient",
                column: "PatOrderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_ReferringPhysician",
                table: "Patient",
                column: "PatReferringPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_RenderingPhysician",
                table: "Patient",
                column: "PatRenderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_SupervisingPhysician",
                table: "Patient",
                column: "PatSupervisingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_BFEPhysician",
                table: "Payment",
                column: "PmtBFEPFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_Patient",
                table: "Payment",
                column: "PmtPatFID",
                principalTable: "Patient",
                principalColumn: "PatID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcedureCode_BillingPhysician",
                table: "Procedure_Code",
                column: "ProcBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceLine_Claim",
                table: "Service_Line",
                column: "SrvClaFID",
                principalTable: "Claim",
                principalColumn: "ClaID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_Payer",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_Payment",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvGUID",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvID",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Adjustment_TaskSrv",
                table: "Adjustment");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_AttendingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_BillingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_FacilityPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_OperatingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_OrderingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_Patient",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_ReferringPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_RenderingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Claim_SupervisingPhy",
                table: "Claim");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_BillingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_FacilityPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_OrderingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_ReferringPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_RenderingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Patient_SupervisingPhysician",
                table: "Patient");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_BFEPhysician",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_Payment_Patient",
                table: "Payment");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcedureCode_BillingPhysician",
                table: "Procedure_Code");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceLine_Claim",
                table: "Service_Line");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_Payer",
                table: "Adjustment",
                column: "AdjPayFID",
                principalTable: "Payer",
                principalColumn: "PayID");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_Payment",
                table: "Adjustment",
                column: "AdjPmtFID",
                principalTable: "Payment",
                principalColumn: "PmtID");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvGUID",
                table: "Adjustment",
                column: "AdjSrvGUID",
                principalTable: "Service_Line",
                principalColumn: "SrvGUID");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_ServiceLine_SrvID",
                table: "Adjustment",
                column: "AdjSrvFID",
                principalTable: "Service_Line",
                principalColumn: "SrvID");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustment_TaskSrv",
                table: "Adjustment",
                column: "AdjTaskFID",
                principalTable: "Service_Line",
                principalColumn: "SrvID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_AttendingPhy",
                table: "Claim",
                column: "ClaAttendingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_BillingPhy",
                table: "Claim",
                column: "ClaBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_FacilityPhy",
                table: "Claim",
                column: "ClaFacilityPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_OperatingPhy",
                table: "Claim",
                column: "ClaOperatingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_OrderingPhy",
                table: "Claim",
                column: "ClaOrderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_Patient",
                table: "Claim",
                column: "ClaPatFID",
                principalTable: "Patient",
                principalColumn: "PatID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_ReferringPhy",
                table: "Claim",
                column: "ClaReferringPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_RenderingPhy",
                table: "Claim",
                column: "ClaRenderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Claim_SupervisingPhy",
                table: "Claim",
                column: "ClaSupervisingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_BillingPhysician",
                table: "Patient",
                column: "PatBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_FacilityPhysician",
                table: "Patient",
                column: "PatFacilityPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_OrderingPhysician",
                table: "Patient",
                column: "PatOrderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_ReferringPhysician",
                table: "Patient",
                column: "PatReferringPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_RenderingPhysician",
                table: "Patient",
                column: "PatRenderingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Patient_SupervisingPhysician",
                table: "Patient",
                column: "PatSupervisingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_BFEPhysician",
                table: "Payment",
                column: "PmtBFEPFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_Patient",
                table: "Payment",
                column: "PmtPatFID",
                principalTable: "Patient",
                principalColumn: "PatID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcedureCode_BillingPhysician",
                table: "Procedure_Code",
                column: "ProcBillingPhyFID",
                principalTable: "Physician",
                principalColumn: "PhyID");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceLine_Claim",
                table: "Service_Line",
                column: "SrvClaFID",
                principalTable: "Claim",
                principalColumn: "ClaID");
        }
    }
}
