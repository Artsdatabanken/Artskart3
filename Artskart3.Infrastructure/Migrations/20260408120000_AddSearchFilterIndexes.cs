using Microsoft.EntityFrameworkCore.Migrations;

namespace Artskart3.Infrastructure.Migrations
{
    public partial class AddSearchFilterIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId",
                table: "Observation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation",
                column: "TaxonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation",
                column: "MonthCollected");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_BasisOfRecordId",
                table: "Observation",
                column: "BasisOfRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation",
                column: "InstitutionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation",
                column: "YearCollected");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_CoordinatePrecisionInMeters",
                table: "Observation",
                column: "CoordinatePrecisionInMeters");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation",
                column: "DateLastModified");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation",
                columns: new[] { "LocationId", "HasErrors", "HasAnnotations" });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation",
                columns: new[] { "YearCollected", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_TaxonGroupId_LocationId",
                table: "Observation",
                columns: new[] { "TaxonGroupId", "LocationId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Observation_TaxonGroupId_LocationId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_CoordinatePrecisionInMeters",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_BasisOfRecordId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId",
                table: "Observation");
        }
    }
}
