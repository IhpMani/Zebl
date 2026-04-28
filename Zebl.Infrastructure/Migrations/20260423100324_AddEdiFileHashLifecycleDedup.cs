using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEdiFileHashLifecycleDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "EdiReport",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdiReport_FileHash",
                table: "EdiReport",
                columns: new[] { "TenantId", "FileHash" },
                unique: true,
                filter: "[FileHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EdiReport_FileHash",
                table: "EdiReport");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "EdiReport");
        }
    }
}
