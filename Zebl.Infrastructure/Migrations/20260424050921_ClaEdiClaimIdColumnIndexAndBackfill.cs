using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClaEdiClaimIdColumnIndexAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Claim_EdiClaimId",
                table: "Claim",
                newName: "UX_Claim_TenantFacility_ClaEdiClaimId");

            migrationBuilder.AlterColumn<string>(
                name: "ClaEdiClaimId",
                table: "Claim",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaEdiClaimId",
                table: "Claim",
                column: "ClaEdiClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Claim_ClaEdiClaimId",
                table: "Claim");

            migrationBuilder.RenameIndex(
                name: "UX_Claim_TenantFacility_ClaEdiClaimId",
                table: "Claim",
                newName: "IX_Claim_EdiClaimId");

            migrationBuilder.AlterColumn<string>(
                name: "ClaEdiClaimId",
                table: "Claim",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
