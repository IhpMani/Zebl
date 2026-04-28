using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenClaimBatchConcurrencyAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ClaimBatchItem",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ClaimBatch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ClaimBatch",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSubmission_ClaimId_SubmissionDate",
                table: "ClaimSubmission",
                columns: new[] { "ClaimId", "SubmissionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatchItem_ClaimId",
                table: "ClaimBatchItem",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatch_TenantFacility_IdempotencyKey",
                table: "ClaimBatch",
                columns: new[] { "TenantId", "FacilityId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClaimSubmission_ClaimId_SubmissionDate",
                table: "ClaimSubmission");

            migrationBuilder.DropIndex(
                name: "IX_ClaimBatchItem_ClaimId",
                table: "ClaimBatchItem");

            migrationBuilder.DropIndex(
                name: "IX_ClaimBatch_TenantFacility_IdempotencyKey",
                table: "ClaimBatch");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ClaimBatchItem");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ClaimBatch");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ClaimBatch");
        }
    }
}
