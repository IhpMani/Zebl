using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimEdiClaimIdMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaEdiClaimId",
                table: "Claim",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claim_EdiClaimId",
                table: "Claim",
                columns: new[] { "TenantId", "FacilityId", "ClaEdiClaimId" },
                unique: true,
                filter: "[ClaEdiClaimId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Claim_EdiClaimId",
                table: "Claim");

            migrationBuilder.DropColumn(
                name: "ClaEdiClaimId",
                table: "Claim");
        }
    }
}
