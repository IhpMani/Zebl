using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <summary>
    /// Backfill existing legacy NULL ClaBillTo values so sendable filtering is deterministic.
    /// </summary>
    public partial class SetClaimBillToBackfill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Claim] SET [ClaBillTo] = 1 WHERE [ClaBillTo] IS NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: restoring NULL would reintroduce non-deterministic sendable filtering.
        }
    }
}

