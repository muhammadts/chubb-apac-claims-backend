using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chubb.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ManagerReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ClaimSettlements_CompletedUtc_ClaimId",
                schema: "dbo",
                table: "ClaimSettlements",
                columns: new[] { "CompletedUtc", "ClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDecisions_DecidedUtc_ClaimId",
                schema: "dbo",
                table: "ClaimDecisions",
                columns: new[] { "DecidedUtc", "ClaimId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClaimSettlements_CompletedUtc_ClaimId",
                schema: "dbo",
                table: "ClaimSettlements");

            migrationBuilder.DropIndex(
                name: "IX_ClaimDecisions_DecidedUtc_ClaimId",
                schema: "dbo",
                table: "ClaimDecisions");
        }
    }
}
