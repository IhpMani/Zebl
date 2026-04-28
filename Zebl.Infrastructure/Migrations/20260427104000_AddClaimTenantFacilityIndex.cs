using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    public partial class AddClaimTenantFacilityIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Claim_Tenant_Facility",
                table: "Claim",
                columns: new[] { "TenantId", "FacilityId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Claim_Tenant_Facility",
                table: "Claim");
        }
    }
}
