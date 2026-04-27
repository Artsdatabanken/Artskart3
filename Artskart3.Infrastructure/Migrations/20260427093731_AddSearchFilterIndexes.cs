using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation",
                column: "DateLastModified");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation",
                column: "InstitutionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation",
                columns: new[] { "LocationId", "HasErrors", "HasAnnotations" });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation",
                columns: new[] { "YearCollected", "LocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation");
        }
    }
}
