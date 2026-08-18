using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFilterColumnsToObservationEntityIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fjern gammel indeks først — unngår indeksvedlikehold under kolonne-tillegg med defaultverdier
        migrationBuilder.DropIndex(
            name: "IX_ObservationEntityIndex_EntityType_EntityId_ObsId",
            table: "ObservationEntityIndex");

        migrationBuilder.AddColumn<int>(
            name: "BasisOfRecordId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "CategoryId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CoordinatePrecisionInMeters",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DateTimeCollected",
            table: "ObservationEntityIndex",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "HasMediaFiles",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<byte>(
            name: "RegistrationStatusId",
            table: "ObservationEntityIndex",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<int>(
            name: "TaxonGroupId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // MERK: Backfill kjøres manuelt via SQL-skript
        // på grunn av lang kjøretid (300M+ rader). Se BackfillObservationEntityIndex.sql.
        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_EntityLookup",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "TaxonGroupId", "CategoryId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ObservationEntityIndex_EntityLookup",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(name: "BasisOfRecordId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "CategoryId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "CoordinatePrecisionInMeters", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "DateTimeCollected", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "HasMediaFiles", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "RegistrationStatusId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "TaxonGroupId", table: "ObservationEntityIndex");

        // Gjenopprett gammel indeks
        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_EntityType_EntityId_ObsId",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "ObservationId" });
    }
}
