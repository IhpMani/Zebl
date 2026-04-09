using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultTenantFacilityIntegrationSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "InboundIntegration", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "InboundIntegration", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "FacilityScope", keyColumn: "FacilityId", keyValue: 1);
            migrationBuilder.DeleteData(table: "FacilityScope", keyColumn: "FacilityId", keyValue: 2);
            migrationBuilder.DeleteData(table: "Tenant", keyColumn: "TenantId", keyValue: 1);
            migrationBuilder.DeleteData(table: "Tenant", keyColumn: "TenantId", keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Tenant",
                columns: new[] { "TenantId", "IsActive", "Name", "TenantKey" },
                values: new object[,]
                {
                    { 1, true, "New Jersey", "nj" },
                    { 2, true, "Michigan", "mi" }
                });

            migrationBuilder.InsertData(
                table: "FacilityScope",
                columns: new[] { "FacilityId", "IsActive", "Name", "TenantId" },
                values: new object[,]
                {
                    { 1, true, "New Jersey (NJ)", 1 },
                    { 2, true, "Michigan (MI)", 2 }
                });

            migrationBuilder.InsertData(
                table: "InboundIntegration",
                columns: new[] { "Id", "FacilityId", "IsActive", "Name", "TenantId" },
                values: new object[,]
                {
                    { 1, 1, true, "NJ HL7", 1 },
                    { 2, 2, true, "MI HL7", 2 }
                });
        }
    }
}
