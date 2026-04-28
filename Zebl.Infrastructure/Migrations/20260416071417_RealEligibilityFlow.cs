using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RealEligibilityFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoinsurancePercent",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "CopayAmount",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "CoverageEndDate",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "CoverageStartDate",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "DeductibleAmount",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "EdiReportId",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "PolicyNumber",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "ResponseReceivedAt",
                table: "EligibilityRequest");

            migrationBuilder.RenameColumn(
                name: "EligibilityRequestId",
                table: "EligibilityResponse",
                newName: "RequestId");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityStatus",
                table: "EligibilityResponse",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "EligibilityResponse",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchFileName",
                table: "EligibilityRequest",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacilityId",
                table: "EligibilityRequest",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubscriberId",
                table: "EligibilityRequest",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EligibilityRequest",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_Status",
                table: "EligibilityRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_SubscriberId",
                table: "EligibilityRequest",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_TenantFacilityPatient",
                table: "EligibilityRequest",
                columns: new[] { "TenantId", "FacilityId", "PatientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EligibilityRequest_Status",
                table: "EligibilityRequest");

            migrationBuilder.DropIndex(
                name: "IX_EligibilityRequest_SubscriberId",
                table: "EligibilityRequest");

            migrationBuilder.DropIndex(
                name: "IX_EligibilityRequest_TenantFacilityPatient",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "EligibilityStatus",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "EligibilityResponse");

            migrationBuilder.DropColumn(
                name: "BatchFileName",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "SubscriberId",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EligibilityRequest");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "EligibilityResponse",
                newName: "EligibilityRequestId");

            migrationBuilder.AddColumn<decimal>(
                name: "CoinsurancePercent",
                table: "EligibilityResponse",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CopayAmount",
                table: "EligibilityResponse",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageEndDate",
                table: "EligibilityResponse",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageStartDate",
                table: "EligibilityResponse",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                table: "EligibilityResponse",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductibleAmount",
                table: "EligibilityResponse",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "EligibilityResponse",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EdiReportId",
                table: "EligibilityRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyNumber",
                table: "EligibilityRequest",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseReceivedAt",
                table: "EligibilityRequest",
                type: "datetime2",
                nullable: true);
        }
    }
}
