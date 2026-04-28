using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistClearinghouseSent837 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SentEdiContent",
                table: "ClaimBatch",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentEdiContent",
                table: "ClaimBatch");
        }
    }
}
