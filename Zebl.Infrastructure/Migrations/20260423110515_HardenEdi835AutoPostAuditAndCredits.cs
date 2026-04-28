using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenEdi835AutoPostAuditAndCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ServiceLineCode",
                table: "ClaimPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldDefaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplyRunId",
                table: "ClaimPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAtUtc",
                table: "ClaimPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostedBy",
                table: "ClaimPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReportId",
                table: "ClaimPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClaimCreditBalance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    SourceReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimCreditBalance_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimPayment_ReportScopedDedup",
                table: "ClaimPayment",
                columns: new[] { "SourceReportId", "TraceNumber", "ClaimExternalId", "PaidAmount", "ServiceLineCode" },
                unique: true,
                filter: "[SourceReportId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimCreditBalance");

            migrationBuilder.DropIndex(
                name: "IX_ClaimPayment_ReportScopedDedup",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "ApplyRunId",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "PostedAtUtc",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "PostedBy",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "SourceReportId",
                table: "ClaimPayment");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceLineCode",
                table: "ClaimPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldDefaultValue: "");
        }
    }
}
