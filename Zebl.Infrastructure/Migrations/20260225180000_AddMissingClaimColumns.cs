using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <summary>
    /// Adds Claim columns that may be missing when the DB was created before migrations or from an older schema.
    /// Uses IF NOT EXISTS so safe to run even if columns already exist.
    /// </summary>
    public partial class AddMissingClaimColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaAdditionalData')
    ALTER TABLE [Claim] ADD [ClaAdditionalData] xml NULL;
");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaClaimType')
    ALTER TABLE [Claim] ADD [ClaClaimType] nvarchar(20) NULL;
");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaPrimaryClaimFID')
    ALTER TABLE [Claim] ADD [ClaPrimaryClaimFID] int NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaAdditionalData')
    ALTER TABLE [Claim] DROP COLUMN [ClaAdditionalData];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaClaimType')
    ALTER TABLE [Claim] DROP COLUMN [ClaClaimType];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Claim') AND name = 'ClaPrimaryClaimFID')
    ALTER TABLE [Claim] DROP COLUMN [ClaPrimaryClaimFID];
");
        }
    }
}
