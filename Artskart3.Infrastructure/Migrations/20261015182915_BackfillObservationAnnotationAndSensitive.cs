using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller ChangedByAnnotation og Sensitive fra taggene de er denormalisert fra.
///
/// Defaultverdien false dekker alt annet, så bare de merkede radene oppdateres:
/// 133 357 for AnnotatedChanges og 2 194 for Sensitive, målt på
/// Artskart3IndexProdLikeTestMigrations. Annoteringsbackfillen tok 2,1 s;
/// sensitivitet er to størrelsesordener mindre. Ingen vannmerker eller oppdeling
/// derfor — i motsetning til BackfillObservationDenormalisedFilterColumns, som
/// måtte gjennom 16,5 millioner rader.
///
/// STATISTIKKOPPDATERINGEN ER PÅKREVD, IKKE VALGFRI
/// 135k endringer på en tabell med 61 millioner rader ligger under terskelen for
/// automatisk oppdatering. Uten denne beholder de filtrerte indeksene
/// statistikken de hadde da de var tomme, optimizeren anslår én rad, og planen
/// blir valgt på feil grunnlag.
///
/// Kjøres etter at indeksene er opprettet (forrige migrasjon), slik at de bygges
/// tomme og fylles av UPDATE-ene her — 135k innsettinger, ikke en indeksbygging
/// over hele tabellen.
/// </summary>
public partial class BackfillObservationAnnotationAndSensitive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

UPDATE o SET o.ChangedByAnnotation = 1
FROM dbo.Observation o
INNER JOIN dbo.ObservationTags ot ON ot.ObservationId = o.Id
WHERE ot.TagId = 9 AND o.ChangedByAnnotation = 0;

UPDATE o SET o.Sensitive = 1
FROM dbo.Observation o
INNER JOIN dbo.ObservationTags ot ON ot.ObservationId = o.Id
WHERE ot.TagId = 10 AND o.Sensitive = 0;

UPDATE STATISTICS dbo.Observation (IX_Observation_ChangedByAnnotation) WITH FULLSCAN;
UPDATE STATISTICS dbo.Observation (IX_Observation_Sensitive) WITH FULLSCAN;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Kolonnene slippes av forrige migrasjons Down. Å nullstille dem her
        // ville bare vært arbeid uten virkning.
    }
}
