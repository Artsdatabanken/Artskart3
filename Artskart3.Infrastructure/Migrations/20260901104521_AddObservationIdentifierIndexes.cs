using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Indekser for eksakt oppslag på ProxyId og OccurrenceId.
///
/// Artskart 2 lot brukeren søke på katalognummer, ProxyId og OccurrenceId under
/// ett. Det gjorde den med LIKE '%x%' over alle tre — nettopp mønsteret som gjorde
/// filteret 18-21 sekunder, og som CompleteFilter fjernet.
///
/// MÅLT PÅ PRODUKSJONSLIK KOPI (61 052 216 rader, 1. september 2026):
///   LIKE '%104168%' på CatalogNumber ..... 4,7 s  (skanner den 949 MB store indeksen)
///   LIKE '%104168%' på ProxyId .......... 12,2 s  (full tabellskann)
///   LIKE '%104168%' på OccurrenceId ..... 29,0 s  (full tabellskann)
/// Ett OR-søk over alle tre gjør én tabellskanning og lander rundt 30 sekunder —
/// på et anonymt endepunkt, altså gratis å misbruke.
///
/// HVORFOR EKSAKT TREFF OG IKKE PREFIKS:
/// De to kolonnene er ikke lengre utgaver av katalognummeret; de er
/// kildekvalifiserte URN-er der katalognummeret ligger til SLUTT:
///   CatalogNumber  37
///   ProxyId        urn:catalog:o:l:37
///   OccurrenceId   urn:catalog:O:L:37
/// Et prefikssøk på «37» treffer derfor ingenting i de to siste. Det eneste
/// prefikssøk finner der, er kildenavn: «urn:catalog:» alene dekker 8 029 416
/// rader. Prefiks er ubrukelig her, og delstreng er for dyrt.
///
/// Eksakt treff er det som faktisk brukes: ingen skriver en halv URN for hånd, de
/// limer inn en hel fra GBIF eller en eksportfil. Det blir et seek på millisekunder.
///
/// Begge kolonnene er tilnærmet unike (60 095 770 og 61 128 229 distinkte verdier),
/// så et treff peker på én observasjon. Databasens sortering er case-insensitiv,
/// så innliming av enten ProxyId eller OccurrenceId finner samme observasjon selv
/// om de to skrives med ulik bokstavstørrelse.
///
/// suppressTransaction av samme grunn som for katalognummerindeksen: byggene går
/// over 61M rader, og en lang transaksjon hindrer avkorting av loggen underveis.
/// IF NOT EXISTS av samme grunn også — blir prosessen drept uten at en attention
/// når serveren, kan indeksen bli stående uten at historikkraden er skrevet, og
/// neste forsøk ville feilet permanent på «There is already an index named ...».
/// </summary>
public partial class AddObservationIdentifierIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Snittlengdene er 41 og 39 tegn mot 10 for katalognummer, så disse to
        // koster mer enn de 949 MB katalognummerindeksen bruker. PAGE-komprimering
        // hjelper godt nettopp her, fordi verdiene deler lange kildeprefikser.
        //
        // MÅLT 1. september 2026 på produksjonslik kopi (61 052 216 rader):
        //   IX_Observation_ProxyId ......... 2 288 MB
        //   IX_Observation_OccurrenceId .... 2 285 MB
        //   IX_Observation_CatalogNumber ...   949 MB (til sammenligning)
        //   Byggetid for de to nye til sammen: ~10 minutter (2 min 16 s for katalog).
        //
        // Komprimeringen slår godt til nettopp fordi verdiene deler lange
        // kildeprefikser: 4x lengre verdier gir bare 2,4x større indeks.
        //
        // Regn med tempdb-plass i tillegg, ikke bare indeksstørrelsen — sorteringen
        // under byggene dro tempdb opp i 5,3 GB, og den krymper ikke av seg selv.
        //
        // Indeksbygging har ingen vannmerker: avbrytes den, er arbeidet tapt og
        // neste forsøk starter på nytt. IF NOT EXISTS beskytter bare tilfellet der
        // bygget FULLFØRTE uten at historikkraden ble skrevet.
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Observation_ProxyId'
                 AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Observation_ProxyId
    ON dbo.Observation (ProxyId)
    WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);
END
", suppressTransaction: true);

        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Observation_OccurrenceId'
                 AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Observation_OccurrenceId
    ON dbo.Observation (OccurrenceId)
    WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);
END
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX IF EXISTS IX_Observation_OccurrenceId ON dbo.Observation;",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "DROP INDEX IF EXISTS IX_Observation_ProxyId ON dbo.Observation;",
            suppressTransaction: true);
    }
}
