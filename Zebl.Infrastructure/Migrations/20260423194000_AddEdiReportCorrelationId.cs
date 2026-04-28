using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

[DbContext(typeof(ZeblDbContext))]
[Migration("20260423194000_AddEdiReportCorrelationId")]
public class AddEdiReportCorrelationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: "EdiReport",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CorrelationId",
            table: "EdiReport");
    }
}

