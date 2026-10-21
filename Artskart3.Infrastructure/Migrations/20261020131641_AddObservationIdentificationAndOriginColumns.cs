using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Denormaliserte kolonner for artsbestemmelse og opprinnelse, på begge tabellene.
///
/// KOBLINGEN TIL TAGGENE
///   UncertainIdentification  ←  taggen UnsureIdentification (2)
///   Introduced               ←  taggen Unspontaneus (1)
///
/// Begge kolonnenavnene avviker fra taggnavnet, og det er med vilje.
/// «Unspontaneus» er feilstavet i både enumen og Tag-tabellen, og den
/// skrivefeilen skal ikke inn i skjema og API. «Introduced» er dessuten det
/// brukeren velger — innført forekomst — mens fravær av taggen er naturlig.
/// Samme frihet er allerede tatt for ChangedByAnnotation, som heter noe annet
/// enn taggen AnnotatedChanges.
///
/// true betyr den MERKEDE halvdelen for begge, som for sensitivitet og
/// annotering: usikker bestemmelse, og innført forekomst.
///
/// HVORFOR BEGGE FÅR FILTRERT INDEKS
/// Skannedybde for side 1 i listevisningen, målt på 61M rader:
///
///   UnsureIdentification   102 388 merket   nyeste 2026-05-14   708 863 skannet
///   Unspontaneus            43 347 merket   nyeste 2026-05-20    24 130 skannet
///
/// Artsbestemmelse trenger indeksen — 708k er halvveis til annotering, som tok
/// 4 545 ms. Opprinnelse hadde trolig klart seg uten: dataene er ferske, så ti
/// treff samler seg etter 24k rader. Den får den likevel, av to grunner. Ett av
/// fire taggfiltre med en annen mekanisme enn de tre andre er en felle for
/// neste leser, og fordelingen kan skifte med import — det er ferskheten, ikke
/// antallet, som gjør den rask i dag.
///
/// Indeksene er små: 43k og 102k rader mot 61 millioner.
///
/// KOSTNAD
/// ALTER TABLE ADD med default er ikke en metadataoperasjon på denne utgaven av
/// SQL Server. Målt: ~6 min per kolonne på Observation (61M rader), noe raskere
/// per kolonne på ObservationEntityIndex (134M rader, men smalere). Fire
/// kolonner her, pluss columnstore-bygget i neste migrasjon.
///
/// Kolonnene er tomme etter denne migrasjonen. Utfylling og columnstore ligger i
/// BackfillObservationIdentificationAndOrigin.
/// </summary>
public partial class AddObservationIdentificationAndOriginColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Introduced",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "UncertainIdentification",
            table: "ObservationEntityIndex",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Introduced",
            table: "Observation",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "UncertainIdentification",
            table: "Observation",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Introduced",
            table: "Observation",
            columns: new[] { "Introduced", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Introduced] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation",
            columns: new[] { "UncertainIdentification", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[UncertainIdentification] = 1");

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION.
        // Samme mønster som de øvrige filtrerte indeksene på Observation.
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_Introduced ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_UncertainIdentification ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_Introduced",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "Introduced",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "UncertainIdentification",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "Introduced",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "UncertainIdentification",
            table: "Observation");
    }
}
