using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateSubmitterReceiverIntoBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FacilityId",
                table: "ReceiverLibrary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ReceiverLibrary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionType",
                table: "ClaimBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmitterReceiverId",
                table: "ClaimBatch",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiverLibrary_TenantFacility_IsActive",
                table: "ReceiverLibrary",
                columns: new[] { "TenantId", "FacilityId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatch_SubmitterReceiverId",
                table: "ClaimBatch",
                column: "SubmitterReceiverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiverLibrary_TenantFacility_IsActive",
                table: "ReceiverLibrary");

            migrationBuilder.DropIndex(
                name: "IX_ClaimBatch_SubmitterReceiverId",
                table: "ClaimBatch");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "ReceiverLibrary");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReceiverLibrary");

            migrationBuilder.DropColumn(
                name: "ConnectionType",
                table: "ClaimBatch");

            migrationBuilder.DropColumn(
                name: "SubmitterReceiverId",
                table: "ClaimBatch");
        }
    }
}
