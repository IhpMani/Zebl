using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryClaimSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaClaimType",
                table: "Claim",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClaPrimaryClaimFID",
                table: "Claim",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SecondaryForwardableAdjustmentRules",
                columns: table => new
                {
                    GroupCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ForwardToSecondary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecondaryForwardableAdjustmentRules", x => new { x.GroupCode, x.ReasonCode });
                });

            // Seed: CO-45 = contractual write-off, never forward. PR-1/2/3 = deductible/coinsurance/copay, usually forward.
            migrationBuilder.InsertData(
                table: "SecondaryForwardableAdjustmentRules",
                columns: new[] { "GroupCode", "ReasonCode", "ForwardToSecondary" },
                values: new object[,]
                {
                    { "CO", "45", false },
                    { "PR", "1", true },
                    { "PR", "2", true },
                    { "PR", "3", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SecondaryForwardableAdjustmentRules");
            migrationBuilder.DropColumn(name: "ClaClaimType", table: "Claim");
            migrationBuilder.DropColumn(name: "ClaPrimaryClaimFID", table: "Claim");
        }
    }
}
