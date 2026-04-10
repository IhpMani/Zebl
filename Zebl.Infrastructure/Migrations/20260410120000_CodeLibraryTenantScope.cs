using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ZeblDbContext))]
[Migration("20260410120000_CodeLibraryTenantScope")]
public class CodeLibraryTenantScope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Remark_Code",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Reason_Code",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Procedure_Code",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Place_of_Service",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Modifier_Code",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TenantId",
            table: "Diagnosis_Code",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.DropIndex(
            name: "IX_Remark_Code_Code",
            table: "Remark_Code");

        migrationBuilder.DropIndex(
            name: "IX_Reason_Code_Code",
            table: "Reason_Code");

        migrationBuilder.DropIndex(
            name: "IX_Procedure_Code_ProcCode_ProcProductCode",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "IX_Place_of_Service_Code",
            table: "Place_of_Service");

        migrationBuilder.DropIndex(
            name: "IX_Modifier_Code_Code",
            table: "Modifier_Code");

        migrationBuilder.DropIndex(
            name: "IX_Diagnosis_Code_Code",
            table: "Diagnosis_Code");

        // Allow tenant-scoped unique indexes: remove duplicates (keep lowest Id) before UX_*.
        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Code, CodeType ORDER BY Id) AS rn
                FROM Diagnosis_Code)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Code ORDER BY Id) AS rn
                FROM Modifier_Code)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Code ORDER BY Id) AS rn
                FROM Place_of_Service)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Code ORDER BY Id) AS rn
                FROM Reason_Code)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Code ORDER BY Id) AS rn
                FROM Remark_Code)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT ProcID, ROW_NUMBER() OVER (PARTITION BY TenantId, ProcCode, ProcProductCode ORDER BY ProcID) AS rn
                FROM Procedure_Code)
            DELETE FROM cte WHERE rn > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Diagnosis_Code_TenantId",
            table: "Diagnosis_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Modifier_Code_TenantId",
            table: "Modifier_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Place_of_Service_TenantId",
            table: "Place_of_Service",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Procedure_Code_TenantId",
            table: "Procedure_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Reason_Code_TenantId",
            table: "Reason_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Remark_Code_TenantId",
            table: "Remark_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "UX_Diagnosis_Code_Tenant_Code_Type",
            table: "Diagnosis_Code",
            columns: new[] { "TenantId", "Code", "CodeType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Modifier_Code_Tenant_Code",
            table: "Modifier_Code",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Place_of_Service_Tenant_Code",
            table: "Place_of_Service",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Procedure_Code_Tenant_ProcCode_Product",
            table: "Procedure_Code",
            columns: new[] { "TenantId", "ProcCode", "ProcProductCode" },
            unique: true,
            filter: "[ProcProductCode] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "UX_Reason_Code_Tenant_Code",
            table: "Reason_Code",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Remark_Code_Tenant_Code",
            table: "Remark_Code",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Diagnosis_Code_Tenant",
            table: "Diagnosis_Code",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Modifier_Code_Tenant",
            table: "Modifier_Code",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Place_of_Service_Tenant",
            table: "Place_of_Service",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Procedure_Code_Tenant",
            table: "Procedure_Code",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Reason_Code_Tenant",
            table: "Reason_Code",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Remark_Code_Tenant",
            table: "Remark_Code",
            column: "TenantId",
            principalTable: "Tenant",
            principalColumn: "TenantId",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Remark_Code_Tenant",
            table: "Remark_Code");

        migrationBuilder.DropForeignKey(
            name: "FK_Reason_Code_Tenant",
            table: "Reason_Code");

        migrationBuilder.DropForeignKey(
            name: "FK_Procedure_Code_Tenant",
            table: "Procedure_Code");

        migrationBuilder.DropForeignKey(
            name: "FK_Place_of_Service_Tenant",
            table: "Place_of_Service");

        migrationBuilder.DropForeignKey(
            name: "FK_Modifier_Code_Tenant",
            table: "Modifier_Code");

        migrationBuilder.DropForeignKey(
            name: "FK_Diagnosis_Code_Tenant",
            table: "Diagnosis_Code");

        migrationBuilder.DropIndex(
            name: "UX_Remark_Code_Tenant_Code",
            table: "Remark_Code");

        migrationBuilder.DropIndex(
            name: "IX_Remark_Code_TenantId",
            table: "Remark_Code");

        migrationBuilder.DropIndex(
            name: "UX_Reason_Code_Tenant_Code",
            table: "Reason_Code");

        migrationBuilder.DropIndex(
            name: "IX_Reason_Code_TenantId",
            table: "Reason_Code");

        migrationBuilder.DropIndex(
            name: "UX_Procedure_Code_Tenant_ProcCode_Product",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "IX_Procedure_Code_TenantId",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "UX_Place_of_Service_Tenant_Code",
            table: "Place_of_Service");

        migrationBuilder.DropIndex(
            name: "IX_Place_of_Service_TenantId",
            table: "Place_of_Service");

        migrationBuilder.DropIndex(
            name: "UX_Modifier_Code_Tenant_Code",
            table: "Modifier_Code");

        migrationBuilder.DropIndex(
            name: "IX_Modifier_Code_TenantId",
            table: "Modifier_Code");

        migrationBuilder.DropIndex(
            name: "UX_Diagnosis_Code_Tenant_Code_Type",
            table: "Diagnosis_Code");

        migrationBuilder.DropIndex(
            name: "IX_Diagnosis_Code_TenantId",
            table: "Diagnosis_Code");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Remark_Code");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Reason_Code");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Procedure_Code");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Place_of_Service");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Modifier_Code");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Diagnosis_Code");

        migrationBuilder.CreateIndex(
            name: "IX_Diagnosis_Code_Code",
            table: "Diagnosis_Code",
            column: "Code");

        migrationBuilder.CreateIndex(
            name: "IX_Modifier_Code_Code",
            table: "Modifier_Code",
            column: "Code");

        migrationBuilder.CreateIndex(
            name: "IX_Place_of_Service_Code",
            table: "Place_of_Service",
            column: "Code");

        migrationBuilder.CreateIndex(
            name: "IX_Procedure_Code_ProcCode_ProcProductCode",
            table: "Procedure_Code",
            columns: new[] { "ProcCode", "ProcProductCode" },
            unique: true,
            filter: "[ProcProductCode] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Reason_Code_Code",
            table: "Reason_Code",
            column: "Code");

        migrationBuilder.CreateIndex(
            name: "IX_Remark_Code_Code",
            table: "Remark_Code",
            column: "Code");
    }
}
