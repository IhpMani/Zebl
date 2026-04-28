using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEdi835AutoPostColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClaimPayment_TransactionDedup",
                table: "ClaimPayment");

            migrationBuilder.AlterColumn<string>(
                name: "PayerId",
                table: "ClaimPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChargeAmount",
                table: "ClaimPayment",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckDateUtc",
                table: "ClaimPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApplied",
                table: "ClaimPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PayerLevel",
                table: "ClaimPayment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceLineCode",
                table: "ClaimPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [ClaimPayment] SET [ServiceLineCode] = '' WHERE [ServiceLineCode] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimPayment_TransactionDedup",
                table: "ClaimPayment",
                columns: new[] { "TraceNumber", "ClaimExternalId", "PaidAmount", "ServiceLineCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClaimPayment_TransactionDedup",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "ChargeAmount",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "CheckDateUtc",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "IsApplied",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "PayerLevel",
                table: "ClaimPayment");

            migrationBuilder.DropColumn(
                name: "ServiceLineCode",
                table: "ClaimPayment");

            migrationBuilder.AlterColumn<int>(
                name: "PayerId",
                table: "ClaimPayment",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimPayment_TransactionDedup",
                table: "ClaimPayment",
                columns: new[] { "TraceNumber", "ClaimExternalId", "PaidAmount" },
                unique: true);
        }
    }
}
