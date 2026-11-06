using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Denormalisert kolonne for funn skjult av annotering (taggen Blocked, 7), på begge
/// tabellene. Filteret er på som standard og må kunne besvares fra tellebufferne, der
/// det er en bit i FilterMask som byggeskriptene leser fra indekstabellen.
///
/// HVORFOR FILTRERT INDEKS
/// 7 267 merkede rader, nyeste 2025-09-26. Uten indeksen må side 1 av «kun skjulte»
/// skanne 1,9 millioner rader nedover datoindeksen. Negasjonen treffer nesten alt og
/// trenger den ikke.
///
/// KOSTNAD
/// ALTER TABLE ADD er ikke en metadataoperasjon her: ~6 min på Observation, målt i
/// AddObservationIdentificationAndOriginColumns. Kolonnene er tomme til
/// BackfillObservationHiddenByAnnotation har kjørt.
/// </summary>
public partial class AddObservationHiddenByAnnotationColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HiddenByAnnotation",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "HiddenByAnnotation",
            table: "Observation",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Observation_HiddenByAnnotation",
            table: "Observation",
            columns: new[] { "HiddenByAnnotation", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[HiddenByAnnotation] = 1")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION.
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_HiddenByAnnotation ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_HiddenByAnnotation",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "HiddenByAnnotation",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "HiddenByAnnotation",
            table: "Observation");
    }
}
