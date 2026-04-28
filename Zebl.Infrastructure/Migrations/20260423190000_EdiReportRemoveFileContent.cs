using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

[DbContext(typeof(ZeblDbContext))]
[Migration("20260423190000_EdiReportRemoveFileContent")]
public class EdiReportRemoveFileContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FileContent",
            table: "EdiReport");

        migrationBuilder.AlterColumn<string>(
            name: "FileStorageKey",
            table: "EdiReport",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "FileStorageKey",
            table: "EdiReport",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500);

        migrationBuilder.AddColumn<byte[]>(
            name: "FileContent",
            table: "EdiReport",
            type: "varbinary(max)",
            nullable: true);
    }
}
