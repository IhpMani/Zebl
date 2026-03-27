using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProcedureCodeProcPayFIDNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcedureCode_Payer",
                table: "Procedure_Code");

            migrationBuilder.AlterColumn<int>(
                name: "ProcPayFID",
                table: "Procedure_Code",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcedureCode_Payer",
                table: "Procedure_Code",
                column: "ProcPayFID",
                principalTable: "Payer",
                principalColumn: "PayID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcedureCode_Payer",
                table: "Procedure_Code");

            migrationBuilder.Sql(
                "UPDATE [Procedure_Code] SET [ProcPayFID] = (SELECT MIN([PayID]) FROM [Payer]) WHERE [ProcPayFID] IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "ProcPayFID",
                table: "Procedure_Code",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcedureCode_Payer",
                table: "Procedure_Code",
                column: "ProcPayFID",
                principalTable: "Payer",
                principalColumn: "PayID");
        }
    }
}
