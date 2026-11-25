using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// BYGGER IKKE LENGER NOE. FASETT-BYGGINGEN LIGGER I
/// 20261125185806_BuildFacetCountCache, og område- og lokasjonsbufferen bygges av
/// develops 20261118131407_RebuildCountCaches.
///
/// Den kjørte Scripts/BuildAreaCountCache.sql, BuildLocationCountCache.sql og
/// BuildFacetCountCache.sql, og var selv den tredje flyttingen av samme migrasjon.
/// Nå er den flyttet en fjerde gang, denne gangen av en annen grunn enn de tre
/// første: ikke fordi noe la seg bak den på egen gren, men fordi develop hadde
/// flyttet SIN byggemigrasjon forbi — 20261118095554_UpdateMdirWmsAdresses kjører rå
/// SQL, og develop la 20261118131407_RebuildCountCaches bak den.
///
/// FASETT-SKRIPTET BLE IKKE LAGT INN I DEVELOPS MIGRASJON
/// Det ble forsøkt, og det var feil. 20261118131407 tilhører develop og er alt
/// anvendt i miljøene; EF har en historikkrad for den og kjører den aldri igjen. Et
/// skript lagt i kroppen hennes ville bare kjørt på ferske databaser, og eksisterende
/// miljøer hadde stått med ubygget fasett-buffer uten at noe feilet. En
/// migrasjonskropp kan ikke endres etter at den er deployert.
///
/// Historikken under står igjen fordi den forklarer HVORFOR regelen finnes.
///
/// HVORFOR REGELEN FINNES
/// En migrasjonskropp er frosset på tidsstempelet sitt, men skriptene den leser
/// vedlikeholdes videre. Ligger en skjemaendring bak byggemigrasjonen, kan en tom
/// database stoppe midt i migreringen — det skjedde én gang, med «Invalid column
/// name 'Mask'». Se 20261007134334_BuildCountCaches.
///
/// Kjeden av flyttinger: 20261007134334_BuildCountCaches →
/// 20261111112446_RebuildCountCaches → 20261116143428_RebuildCountCachesWithFacets →
/// hit → 20261125185806_BuildFacetCountCache.
///
/// <c>CountCacheBuildMigrationTests</c> håndhever regelen, og aliaset
/// <c>Byggemigrasjonen</c> der peker nå på 20261125185806_BuildFacetCountCache.
///
/// PRISEN ER ETT NYTT BYGG PER MILJØ
/// Et miljø som alt har kjørt denne bygger fasett-bufferen om. Det er bevisst: en
/// buffer som bygges for mye er billigere enn en migreringskjede som stopper på en tom
/// database.
///
/// MERK at bufferne uansett MÅ bygges om etter sammenslåingen. HiddenByAnnotation ble
/// maskens sjette bit, og RowLayoutVersion gikk fra 2 til 3 — i en eldre buffer er
/// biten alltid 0, så skjulte funn ville blitt talt med i standardfilteret.
/// </summary>
public partial class RebuildCountCachesAfterTaxonRanks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // TØMT, som byggemigrasjonene før den, og av samme grunn.
        //
        // Skal bufferne bygges av en migrasjon, hører det i den nyeste
        // byggemigrasjonen — 20261118131407_RebuildCountCaches — ikke her.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er avledede data, ikke skjema. Å tømme bufferne ved en
        // nedmigrering ville bare gjort spørringene tregere — tabellene slippes av
        // migrasjonene som opprettet dem.
    }
}
