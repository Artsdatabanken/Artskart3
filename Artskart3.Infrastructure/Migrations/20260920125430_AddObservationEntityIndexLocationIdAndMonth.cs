using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// LocationId på ObservationEntityIndex, denormalisert fra Observation.
///
/// HVORFOR
/// Lokasjonssøket grupperer på LocationId. Kjørt mot Observation gikk
/// aggregeringen i row mode — målt på Oslo-utsnittet 11 166 ms og 84 sekunder
/// CPU for å gruppere 57M rader. Indekstabellen har en columnstore og alle
/// filterkolonnene fra før, men manglet grupperingsnøkkelen, så spørringen måtte
/// joine tilbake til Observation og falt dermed ut av batch mode.
///
/// Målt på samme datamengde og gruppeantall:
///   dagens sti, via Observation                 11 166 ms
///   indekstabellen + join for LocationId          5 116 ms
///   ren columnstore-aggregering, 400 000 grupper  1 415 ms
///
/// Joinen kostet altså 3,7 av de 5,1 sekundene. Denne kolonnen fjerner den.
///
/// Kolonnen er nullable: en observasjon uten lokalitet har ingen LocationId, og
/// NULL er riktig sluttilstand for dem. Nullable uten default er dessuten en ren
/// metadataoperasjon — se AddCompleteFilterColumns.
///
/// Kolonnen fylles av BackfillObservationEntityIndexLocationId, og legges inn i
/// IX_OEI_Columnstore av samme migrasjon. Før den er kjørt er kolonnen NULL, og
/// lokasjonssøket returnerer tomt — de to hører sammen og må deployes sammen.
///
/// MONTHCOLLECTED HENGER PÅ
/// Migrasjonen bærer også MonthCollected. Navnet nevner den ikke, og det er et
/// bevisst valg: migrasjonsnavn står i __EFMigrationsHistory, og et bytte ville
/// fått EF til å tro at migrasjonen ikke var kjørt på databaser som allerede har
/// den. Grunnen til at den ligger her er at BackfillObservationEntityIndexLocationId
/// uansett river og bygger IX_OEI_Columnstore på nytt — 10-30 minutter. En egen
/// migrasjon for MonthCollected ville betalt den prisen en gang til for én kolonne.
/// Se kolonnens doc-kommentar på ObservationAreaIndex for målingene.
/// </summary>
public partial class AddObservationEntityIndexLocationIdAndMonth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'LocationId') IS NULL
    ALTER TABLE dbo.ObservationEntityIndex ADD LocationId INT NULL;
", suppressTransaction: true);

        // Egen Sql() og ikke to setninger i samme blokk: neste migrasjon
        // refererer MonthCollected i en UPDATE, og kolonnen maa vaere synlig
        // naar den batchen kompileres.
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'MonthCollected') IS NULL
    ALTER TABLE dbo.ObservationEntityIndex ADD MonthCollected TINYINT NULL;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'MonthCollected') IS NOT NULL
    ALTER TABLE dbo.ObservationEntityIndex DROP COLUMN MonthCollected;
", suppressTransaction: true);

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'LocationId') IS NOT NULL
    ALTER TABLE dbo.ObservationEntityIndex DROP COLUMN LocationId;
", suppressTransaction: true);
    }
}
