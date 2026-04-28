using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zebl.Infrastructure.Persistence.Context;

#nullable disable

namespace Zebl.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ZeblDbContext))]
[Migration("20260413150000_ProcedureCodeFacilityScope")]
public class ProcedureCodeFacilityScope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FacilityId",
            table: "Procedure_Code",
            type: "int",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE pc
            SET FacilityId = p.FacilityId
            FROM Procedure_Code pc
            INNER JOIN Physician p ON p.PhyID = pc.ProcBillingPhyFID AND p.TenantId = pc.TenantId
            WHERE pc.FacilityId IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE pc
            SET FacilityId = sub.FacilityId
            FROM Procedure_Code pc
            CROSS APPLY (
                SELECT TOP (1) fs.FacilityId
                FROM FacilityScope fs
                WHERE fs.TenantId = pc.TenantId
                ORDER BY fs.FacilityId) sub
            WHERE pc.FacilityId IS NULL;
            """);

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM Procedure_Code WHERE FacilityId IS NULL)
            BEGIN
                THROW 50001, 'Procedure_Code.FacilityId backfill failed: add FacilityScope rows for affected tenants.', 1;
            END
            """);

        migrationBuilder.Sql("""
            ;WITH cte AS (
                SELECT ProcID,
                    ROW_NUMBER() OVER (
                        PARTITION BY TenantId, FacilityId, ProcCode, COALESCE(ProcProductCode, N'')
                        ORDER BY ProcID) AS rn
                FROM Procedure_Code)
            DELETE FROM Procedure_Code WHERE ProcID IN (SELECT ProcID FROM cte WHERE rn > 1);
            """);

        migrationBuilder.AlterColumn<int>(
            name: "FacilityId",
            table: "Procedure_Code",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "UX_Procedure_Code_Tenant_ProcCode_Product",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "IX_Procedure_Code_TenantId",
            table: "Procedure_Code");

        migrationBuilder.CreateIndex(
            name: "IX_Procedure_Code_Tenant_Facility",
            table: "Procedure_Code",
            columns: new[] { "TenantId", "FacilityId" });

        migrationBuilder.CreateIndex(
            name: "UX_Procedure_Code_Tenant_Facility_ProcCode_Product",
            table: "Procedure_Code",
            columns: new[] { "TenantId", "FacilityId", "ProcCode", "ProcProductCode" },
            unique: true,
            filter: "[ProcProductCode] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_Procedure_Code_FacilityScope",
            table: "Procedure_Code",
            column: "FacilityId",
            principalTable: "FacilityScope",
            principalColumn: "FacilityId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(
            name: "IX_Procedure_Code_FacilityId",
            table: "Procedure_Code",
            column: "FacilityId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Procedure_Code_FacilityScope",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "IX_Procedure_Code_FacilityId",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "UX_Procedure_Code_Tenant_Facility_ProcCode_Product",
            table: "Procedure_Code");

        migrationBuilder.DropIndex(
            name: "IX_Procedure_Code_Tenant_Facility",
            table: "Procedure_Code");

        migrationBuilder.CreateIndex(
            name: "IX_Procedure_Code_TenantId",
            table: "Procedure_Code",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "UX_Procedure_Code_Tenant_ProcCode_Product",
            table: "Procedure_Code",
            columns: new[] { "TenantId", "ProcCode", "ProcProductCode" },
            unique: true,
            filter: "[ProcProductCode] IS NOT NULL");

        migrationBuilder.DropColumn(
            name: "FacilityId",
            table: "Procedure_Code");
    }
}
