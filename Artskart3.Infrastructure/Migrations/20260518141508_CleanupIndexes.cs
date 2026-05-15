using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogNumber",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_CategoryId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_DateTimeCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_DatetimeIdentified",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_DateTimeRecordImported",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_LocationCatProxy",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_MonthCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_NodeId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_OccurenceId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_ProxyId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_TaxonId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_YearCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "TaxonGroupId",
                table: "Observation");

            migrationBuilder.RenameIndex(
                name: "IX_TaxonGroupIdLocationId",
                table: "Observation",
                newName: "IX_Observation_TaxonGroupId_LocationId");

            migrationBuilder.RenameIndex(
                name: "IX_ObservationQualityTypeId",
                table: "Observation",
                newName: "IX_Observation_ObservationQualityTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_MatchedScientificName",
                table: "Observation",
                newName: "IX_Observation_MatchedScientificNameId");

            migrationBuilder.RenameIndex(
                name: "Ix_CoordPrec",
                table: "Observation",
                newName: "IX_Observation_CoordinatePrecisionInMeters");

            migrationBuilder.RenameIndex(
                name: "IX_BasisOfRecordId",
                table: "Observation",
                newName: "IX_Observation_BasisOfRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Observation_TaxonGroupId_LocationId",
                table: "Observation",
                newName: "IX_TaxonGroupIdLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_Observation_ObservationQualityTypeId",
                table: "Observation",
                newName: "IX_ObservationQualityTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Observation_MatchedScientificNameId",
                table: "Observation",
                newName: "IX_MatchedScientificName");

            migrationBuilder.RenameIndex(
                name: "IX_Observation_CoordinatePrecisionInMeters",
                table: "Observation",
                newName: "Ix_CoordPrec");

            migrationBuilder.RenameIndex(
                name: "IX_Observation_BasisOfRecordId",
                table: "Observation",
                newName: "IX_BasisOfRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogNumber",
                table: "Observation",
                column: "CatalogNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryId",
                table: "Observation",
                columns: new[] { "CategoryId", "DateTimeRecordImported" });

            migrationBuilder.CreateIndex(
                name: "IX_DateTimeCollected",
                table: "Observation",
                column: "DateTimeCollected",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DatetimeIdentified",
                table: "Observation",
                column: "DatetimeIdentified");

            migrationBuilder.CreateIndex(
                name: "IX_DateTimeRecordImported",
                table: "Observation",
                column: "DateTimeRecordImported");

            migrationBuilder.CreateIndex(
                name: "IX_LocationCatProxy",
                table: "Observation",
                columns: new[] { "LocationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthCollected",
                table: "Observation",
                column: "MonthCollected");

            migrationBuilder.CreateIndex(
                name: "IX_NodeId",
                table: "Observation",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OccurenceId",
                table: "Observation",
                column: "OccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyId",
                table: "Observation",
                column: "ProxyId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxonId",
                table: "Observation",
                columns: new[] { "TaxonId", "MatchedScientificNameId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearCollected",
                table: "Observation",
                columns: new[] { "YearCollected", "MonthCollected" });

            migrationBuilder.CreateIndex(
                name: "TaxonGroupId",
                table: "Observation",
                column: "TaxonGroupId");
        }
    }
}
