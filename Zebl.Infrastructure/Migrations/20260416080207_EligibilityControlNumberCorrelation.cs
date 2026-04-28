using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EligibilityControlNumberCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControlNumber",
                table: "EligibilityRequest",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_ControlNumber",
                table: "EligibilityRequest",
                column: "ControlNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EligibilityRequest_ControlNumber",
                table: "EligibilityRequest");

            migrationBuilder.DropColumn(
                name: "ControlNumber",
                table: "EligibilityRequest");
        }
    }
}
