using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeEdi835EdgeCaseHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReversed",
                table: "ClaimPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PaymentBatchId",
                table: "ClaimPayment",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "ClaimPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TakebackAmount",
                table: "ClaimPayment",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentBatch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    TraceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CheckDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatch_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimPayment_TraceClaimLine",
                table: "ClaimPayment",
                columns: new[] { "TraceNumber", "ClaimExternalId", "ServiceLineCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_TenantFacilityTrace",
                table: "PaymentBatch",
                columns: new[] { "TenantId", "FacilityId", "TraceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentBatch");

            migrationBuilder.DropIndex(
                name: "IX_ClaimPayment_TraceClaimLine",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "IsReversed",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "PaymentBatchId",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "TakebackAmount",
                table: "ClaimPayment");
        }
    }
}
