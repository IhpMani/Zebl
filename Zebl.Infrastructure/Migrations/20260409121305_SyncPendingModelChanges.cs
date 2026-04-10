using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalProviderId",
                table: "Physician",
                type: "varchar(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Physician_ExternalProvider",
                table: "Physician",
                columns: new[] { "TenantId", "FacilityId", "ExternalProviderId" },
                unique: true,
                filter: "[ExternalProviderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Physician_ExternalProvider",
                table: "Physician");

            migrationBuilder.DropColumn(
                name: "ExternalProviderId",
                table: "Physician");
        }
    }
}
