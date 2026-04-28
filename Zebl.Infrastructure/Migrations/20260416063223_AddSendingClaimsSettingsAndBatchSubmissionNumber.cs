using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSendingClaimsSettingsAndBatchSubmissionNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionNumber",
                table: "ClaimBatch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SendingClaimsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ShowBillToPatientClaims = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PatientControlNumberMode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "ClaimId"),
                    NextSubmissionNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendingClaimsSettings_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SendingClaimsSettings_Tenant_Facility",
                table: "SendingClaimsSettings",
                columns: new[] { "TenantId", "FacilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SendingClaimsSettings");

            migrationBuilder.DropColumn(
                name: "SubmissionNumber",
                table: "ClaimBatch");
        }
    }
}
