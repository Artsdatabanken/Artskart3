using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tar annotering og sensitivitet inn på indekstabellen, slik Observation
/// allerede har dem.
///
/// HVORFOR
/// Begge filtrene er tagger, og stiene som drives av ObservationEntityIndex
/// måtte derfor semi-joine mot ObservationTags. Målt på en Full-kjøring over
/// 8 271 målinger, AreaCounts med ett enkelt filter:
///
///   ingen filter                      6 ms   (buffer)
///   bilder:lett / :tung            7 / 9 ms  (buffer — er en bufferdimensjon)
///   annotering:lett / :tung    280 / 769 ms  (semi-join, buffer forbigått)
///   sensitivitet:lett / :tung   39 / 759 ms  (semi-join, buffer forbigått)
///
/// Negasjonen er den dyre: NOT EXISTS mot 61 millioner rader, uten at bufferen
/// kan svare. Kolonnene her fjerner semi-joinen. De fjerner IKKE
/// bufferforbigåelsen — til det må de også bli dimensjoner i selve bufferen, og
/// det er en større endring (pargruppene går fra 78 til 105, og #dim-listene i
/// begge byggeskriptene må følge med). Se vaktene i AreaCountCacheService,
/// LocationCountCacheService og PolygonLocationStore.
///
/// KOLONNENE ER TOMME ETTER DENNE MIGRASJONEN
/// Utfyllingen og columnstore-bygget ligger i
/// BackfillObservationEntityIndexAnnotationAndSensitive, etter mønsteret fra
/// AddObservationEntityIndexLocationIdAndMonth. Kolonnen uten columnstore gir
/// ingen gevinst — hele poenget er at aggregeringen skal gå i batch mode.
///
/// KOSTNAD
/// ALTER TABLE ADD med default er ikke en ren metadataoperasjon på denne
/// utgaven av SQL Server. Målt på Observation (61M rader) tok hver kolonne
/// omtrent seks minutter med skjemalås på tabellen; ObservationEntityIndex har
/// ~134M rader, så regn med mer. Det er ikke en rutineendring i produksjon.
/// </summary>
public partial class AddObservationEntityIndexAnnotationAndSensitive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ChangedByAnnotation",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Sensitive",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ChangedByAnnotation",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "Sensitive",
            table: "ObservationEntityIndex");
    }
}
