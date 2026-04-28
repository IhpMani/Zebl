using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddControlNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlNumberSequence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    LastInterchangeNumber = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    LastGroupNumber = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    LastTransactionNumber = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlNumberSequence_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlNumberSequence_Tenant_Facility",
                table: "ControlNumberSequence",
                columns: new[] { "TenantId", "FacilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlNumberSequence");
        }
    }
}
