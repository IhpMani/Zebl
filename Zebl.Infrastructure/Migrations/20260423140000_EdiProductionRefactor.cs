using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

[DbContext(typeof(ZeblDbContext))]
[Migration("20260423140000_EdiProductionRefactor")]
public class EdiProductionRefactor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ConnectionType",
            table: "ConnectionLibrary",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "InboundFetchPath",
            table: "ConnectionLibrary",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Host",
            table: "ConnectionLibrary",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(255)",
            oldMaxLength: 255);

        migrationBuilder.AddColumn<string>(
            name: "FileStorageKey",
            table: "EdiReport",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentHashSha256",
            table: "EdiReport",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AlterColumn<byte[]>(
            name: "FileContent",
            table: "EdiReport",
            type: "varbinary(max)",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "varbinary(max)",
            oldNullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_EdiReport_InboundDedup",
            table: "EdiReport",
            columns: new[] { "TenantId", "ReceiverLibraryId", "ConnectionLibraryId", "ContentHashSha256" },
            unique: true,
            filter: "[ContentHashSha256] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EdiReport_InboundDedup",
            table: "EdiReport");

        migrationBuilder.DropColumn(
            name: "FileStorageKey",
            table: "EdiReport");

        migrationBuilder.DropColumn(
            name: "ContentHashSha256",
            table: "EdiReport");

        migrationBuilder.Sql(
            "UPDATE [EdiReport] SET [FileContent] = CONVERT(VARBINARY(MAX), '') WHERE [FileContent] IS NULL");

        migrationBuilder.AlterColumn<byte[]>(
            name: "FileContent",
            table: "EdiReport",
            type: "varbinary(max)",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "varbinary(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Host",
            table: "ConnectionLibrary",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000);

        migrationBuilder.DropColumn(
            name: "InboundFetchPath",
            table: "ConnectionLibrary");

        migrationBuilder.DropColumn(
            name: "ConnectionType",
            table: "ConnectionLibrary");
    }
}
