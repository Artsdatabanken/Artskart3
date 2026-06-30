using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ImproveFilterQueryIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Erstatter IX_ObservationEntityIndex_Lookup (EntityTypeId, EntityId) — nytt indeks er et superset.
        migrationBuilder.DropIndex(
            name: "IX_ObservationEntityIndex_Lookup",
            table: "ObservationEntityIndex");

        // Erstatter IX_ObservationEntityIndex_ObservationId — PK (ObservationId, EntityTypeId, EntityId) dekker allerede.
        migrationBuilder.DropIndex(
            name: "IX_ObservationEntityIndex_ObservationId",
            table: "ObservationEntityIndex");

        // Covering composite for filtered area/location/observation queries via ObservationEntityIndex.
        // Erstatter begge fjernede indekser og støtter alle eksisterende spørringsmønstre.
        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_EntityType_EntityId_ObsId",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "ObservationId" });

        // Periodefiltrering (WHERE DateTimeCollected >= @from AND <= @to) mangler indeks.
        migrationBuilder.CreateIndex(
            name: "IX_Observation_DateTimeCollected",
            table: "Observation",
            column: "DateTimeCollected");

        // GetAreaMarkersAsync filtrerer på (ZoomLevel, IsCurrent) men har ingen indeks.
        migrationBuilder.CreateIndex(
            name: "IX_Area_ZoomLevel_IsCurrent",
            table: "Area",
            columns: new[] { "ZoomLevel", "IsCurrent" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ObservationEntityIndex_EntityType_EntityId_ObsId",
            table: "ObservationEntityIndex");

        migrationBuilder.DropIndex(
            name: "IX_Observation_DateTimeCollected",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Area_ZoomLevel_IsCurrent",
            table: "Area");

        // Gjenopprett de fjernede indeksene
        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_Lookup",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_ObservationId",
            table: "ObservationEntityIndex",
            column: "ObservationId");
    }
}
