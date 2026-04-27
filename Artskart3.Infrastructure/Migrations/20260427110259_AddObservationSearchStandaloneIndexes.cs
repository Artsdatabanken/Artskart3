using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationSearchStandaloneIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId",
                table: "Observation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation",
                column: "MonthCollected");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation",
                column: "TaxonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation",
                column: "YearCollected");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation");
        }
    }
}
