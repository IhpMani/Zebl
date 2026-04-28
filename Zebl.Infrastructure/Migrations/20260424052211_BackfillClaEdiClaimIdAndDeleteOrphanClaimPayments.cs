using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <summary>
    /// Aligns Claim.ClaEdiClaimId with payer CLP01 (often same as ClaExternalFID / prior CLM01).
    /// Removes pre-fix ClaimPayment rows that never matched a claim (ClaimId NULL).
    /// </summary>
    public partial class BackfillClaEdiClaimIdAndDeleteOrphanClaimPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: populate EDI claim id from external FID (matches typical test 835 CLP01 values like 10001).
            migrationBuilder.Sql("""
                UPDATE [Claim]
                SET [ClaEdiClaimId] = LEFT(LTRIM(RTRIM(CAST([ClaExternalFID] AS NVARCHAR(50)))), 50)
                WHERE [ClaEdiClaimId] IS NULL
                  AND [ClaExternalFID] IS NOT NULL
                  AND LTRIM(RTRIM([ClaExternalFID])) <> '';
                """);

            // Step 2: remaining rows — use internal id so new 837/835 round-trip still works.
            migrationBuilder.Sql("""
                UPDATE [Claim]
                SET [ClaEdiClaimId] = CAST([ClaID] AS NVARCHAR(50))
                WHERE [ClaEdiClaimId] IS NULL;
                """);

            // Step 3: orphan payment lines from before mapping fix (re-import after this if needed).
            migrationBuilder.Sql("""
                DELETE FROM [ClaimPayment]
                WHERE [ClaimId] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
