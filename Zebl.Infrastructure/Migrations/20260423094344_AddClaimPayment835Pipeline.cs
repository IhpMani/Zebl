using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPayment835Pipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EdiReport_InboundDedup",
                table: "EdiReport");

            migrationBuilder.AddColumn<string>(
                name: "ClaimIdentifier",
                table: "EdiReport",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClaimPayment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ClaimId = table.Column<int>(type: "int", nullable: true),
                    ClaimExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TraceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCharge = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PatientResponsibility = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PayerId = table.Column<int>(type: "int", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOrphan = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimPayment_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimPayment_TransactionDedup",
                table: "ClaimPayment",
                columns: new[] { "TraceNumber", "ClaimExternalId", "PaidAmount" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "ClaimIdentifier",
                table: "EdiReport");

            migrationBuilder.CreateIndex(
                name: "IX_EdiReport_InboundDedup",
                table: "EdiReport",
                columns: new[] { "TenantId", "ReceiverLibraryId", "ConnectionLibraryId", "ContentHashSha256" },
                unique: true,
                filter: "[ContentHashSha256] IS NOT NULL");
        }
    }
}
