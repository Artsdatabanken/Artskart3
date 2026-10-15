using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Denormaliserte kolonner og filtrerte indekser for annotering og sensitivitet.
///
/// HVA DET LØSER
/// Begge filtrene er tagger, og den SELEKTIVE halvdelen er den trege — ikke
/// negasjonen, slik man skulle tro. Listevisningen sorterer på
/// DateTimeCollected og tar TOP 10, så arbeidet er hvor langt ned i datoindeksen
/// spørringen må gå før ti treff har samlet seg, med et nøkkeloppslag og en
/// probe mot ObservationTags på hver eneste rad underveis.
///
/// Målt på Artskart3IndexProdLikeTestMigrations (61M rader):
///
///   Tagg                     Merkede rader   Nyeste      Skannet for side 1
///   AnnotatedChanges (9)           133 357   2025-11-02           1 504 153
///   Sensitive (10)                   2 194   2025-07-01           6 338 836
///
/// changedByAnnotation = true tok 4 545 ms mot 19 ms for sin egen negasjon.
/// Sensitive må skanne 4,2 ganger så mange rader som annotering.
///
/// HVORFOR FILTRERT
/// Negasjonen trenger ingen indeks: nesten alle rader treffer, så TOP 10 stopper
/// med én gang, og den er allerede 19 ms på datoindeksen. Bare de merkede radene
/// er problemet, og en filtrert indeks dekker nettopp dem — 135k rader i stedet
/// for 61 millioner. Samme mønster og samme begrunnelse som
/// IX_Observation_RegistrationStatusRare; se kommentarblokken der.
///
/// Å legge til kolonnene er en ren metadataoperasjon, og indeksene opprettes
/// tomme. Selve utfyllingen skjer i BackfillObservationAnnotationAndSensitive.
///
/// QUOTED_IDENTIFIER
/// Filtrerte indekser krever SET QUOTED_IDENTIFIER ON ved både lesing og
/// skriving. .NET SqlClient har den på. En INSERT eller UPDATE mot Observation
/// fra en forbindelse som har den AV vil feile — det gjelder importjobben når
/// den skrives. Prosjektet har allerede filtrerte indekser på
/// ObservationTaxonHierarchy og Observation, så mønsteret er etablert.
///
/// IKKE PÅ ObservationEntityIndex
/// Kolonnene finnes bare på Observation. Områdetellingene bruker fortsatt
/// semi-joinen mot ObservationTags (FilterOnTag), som allerede er rask nok
/// (205 ms / 389 ms målt). Så lenge indekstabellen mangler kolonnene, må
/// AreaCountCacheService, LocationCountCacheService og PolygonLocationStore
/// falle gjennom på disse to filtrene — ellers svarer bufferne for høyt.
/// </summary>
public partial class AddObservationAnnotationAndSensitiveColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ChangedByAnnotation",
            table: "Observation",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Sensitive",
            table: "Observation",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation",
            columns: new[] { "ChangedByAnnotation", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[ChangedByAnnotation] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation",
            columns: new[] { "Sensitive", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Sensitive] = 1");

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION.
        // Samme mønster som IX_Observation_RegistrationStatusRare.
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_ChangedByAnnotation ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_Sensitive ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "ChangedByAnnotation",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "Sensitive",
            table: "Observation");
    }
}
