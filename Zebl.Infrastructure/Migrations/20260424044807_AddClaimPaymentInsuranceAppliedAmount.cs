using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPaymentInsuranceAppliedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceAppliedAmount",
                table: "ClaimPayment",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Historical rows that already posted to the claim ledger used PaidAmount as the applied insurance.
            migrationBuilder.Sql(
                "UPDATE [ClaimPayment] SET [InsuranceAppliedAmount] = [PaidAmount] WHERE [IsApplied] = 1 AND [InsuranceAppliedAmount] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceAppliedAmount",
                table: "ClaimPayment");
        }
    }
}
