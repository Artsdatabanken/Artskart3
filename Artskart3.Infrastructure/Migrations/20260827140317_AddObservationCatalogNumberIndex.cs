using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// CompleteFilter — indeksen som betjener typeahead-endepunktet for katalognummer.
///
/// Egen migrasjon fordi den skiller seg fra skjemaendringen foran: den bygges over
/// 61 052 216 rader og må kjøre utenfor migrasjonstransaksjonen. En så lang
/// operasjon inne i en transaksjon hindrer avkorting av transaksjonsloggen mens
/// den pågår, noe som på Azure SQL (alltid full recovery) kan blåse opp loggen —
/// og en feil ville gitt en tilsvarende lang rollback. Samme resonnement som
/// migrasjon 20260820150000 for columnstore-indeksen.
///
/// HVORFOR DENNE INDEKSEN, OG HVA DEN IKKE ER FOR:
/// Katalognummer er tilnærmet unikt — 54 687 713 distinkte verdier over 61M
/// observasjoner, ingen tomme, og flest observasjoner på samme verdi er 675.
/// Å denormalisere en så høykardinal streng inn i 192M columnstore-rader ville
/// vært det verste alternativet; strengordbøker er nettopp det columnstore
/// håndterer dårligst.
///
/// I stedet gjør typeahead-endepunktet prefikssøk mot denne indeksen, og frontend
/// sender ObservationId-ene brukeren velger. Filterspørringen gjør da et seek på
/// den clustered indeksen til ObservationEntityIndex og trenger aldri
/// strengsammenligning. Indeksen er altså for oppslaget, ikke for filteret.
///
/// MERK: prefikssøk (LIKE 'x%') kan bruke indeksen. Delstrengsøk (LIKE '%x%')
/// kan det ikke, og var nettopp det som gjorde filteret 18-21 s i utgangspunktet.
/// Endres søket tilbake til delstreng, forsvinner gevinsten.
/// </summary>
public partial class AddObservationCatalogNumberIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IF NOT EXISTS er nødvendig, ikke defensivt pynt. Bygget tar lang tid over
        // 61M rader, og migrasjonen forventes å bli avbrutt. Ved en klienttimeout
        // rulles bygget tilbake, men blir prosessen drept (container-SIGKILL) uten
        // at en attention når serveren, kjører CREATE INDEX ofte ferdig likevel.
        // Historikkraden er da ikke skrevet, og neste forsøk ville feilet permanent
        // med «There is already an index named ...» — og blokkert alle migrasjoner
        // etter denne.
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Observation_CatalogNumber'
                 AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Observation_CatalogNumber
    ON dbo.Observation (CatalogNumber)
    WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);
END
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX IF EXISTS IX_Observation_CatalogNumber ON dbo.Observation;",
            suppressTransaction: true);
    }
}
