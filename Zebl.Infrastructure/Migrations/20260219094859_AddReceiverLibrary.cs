using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiverLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiverLibrary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LibraryEntryName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExportFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmitterType = table.Column<int>(type: "int", nullable: false),
                    BusinessOrLastName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SubmitterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactValue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceiverId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorizationInfo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SecurityInfo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SenderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InterchangeReceiverId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AcknowledgeRequested = table.Column<bool>(type: "bit", nullable: false),
                    TestProdIndicator = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    SenderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiverCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiverLibrary_Id", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiverLibrary_LibraryEntryName",
                table: "ReceiverLibrary",
                column: "LibraryEntryName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiverLibrary_LibraryEntryName",
                table: "ReceiverLibrary");

            migrationBuilder.DropTable(
                name: "ReceiverLibrary");
        }
    }
}
