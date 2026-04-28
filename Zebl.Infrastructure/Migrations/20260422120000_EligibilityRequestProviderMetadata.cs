using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ZeblDbContext))]
[Migration("20260422120000_EligibilityRequestProviderMetadata")]
public class EligibilityRequestProviderMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProviderNpi",
            table: "EligibilityRequest",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderMode",
            table: "EligibilityRequest",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "UsedPayerOverride",
            table: "EligibilityRequest",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ProviderNpi", table: "EligibilityRequest");
        migrationBuilder.DropColumn(name: "ProviderMode", table: "EligibilityRequest");
        migrationBuilder.DropColumn(name: "UsedPayerOverride", table: "EligibilityRequest");
    }
}
