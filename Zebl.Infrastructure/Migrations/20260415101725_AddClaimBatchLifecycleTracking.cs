using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimBatchLifecycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimBatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalClaims = table.Column<int>(type: "int", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimBatch_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClaimBatchItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimBatchItem_Id", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimBatchItem_ClaimBatch",
                        column: x => x.BatchId,
                        principalTable: "ClaimBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatch_Status",
                table: "ClaimBatch",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatch_TenantFacility_CreatedAt",
                table: "ClaimBatch",
                columns: new[] { "TenantId", "FacilityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatchItem_BatchId",
                table: "ClaimBatchItem",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatchItem_BatchId_ClaimId",
                table: "ClaimBatchItem",
                columns: new[] { "BatchId", "ClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatchItem_Status",
                table: "ClaimBatchItem",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimBatchItem");

            migrationBuilder.DropTable(
                name: "ClaimBatch");
        }
    }
}
