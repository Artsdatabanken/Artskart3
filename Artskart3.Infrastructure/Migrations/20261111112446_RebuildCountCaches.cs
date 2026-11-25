using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// BYGGER IKKE LENGER NOE. BYGGINGEN LIGGER I
/// 20261116143428_RebuildCountCachesWithFacets.
///
/// Migrasjonen kjørte Scripts/BuildAreaCountCache.sql og BuildLocationCountCache.sql,
/// lest som innebygde ressurser — den var selv flyttingen bort fra
/// 20261007134334_BuildCountCaches, som stoppet en tom database med «Invalid column
/// name 'Mask'». Se den for hele historien.
///
/// HVORFOR DEN MÅTTE FLYTTES IGJEN
/// Samme regel, samme grunn: en migrasjonskropp er frosset på tidsstempelet sitt,
/// mens skriptene den leser vedlikeholdes videre. 20261116135633_AddFacetCountCacheTables
/// ligger etter denne og oppretter tabeller, og da er forutsetningen om at ingenting
/// bak byggemigrasjonen endrer skjemaet brutt.
///
/// <c>CountCacheBuildMigrationTests.Etter_byggemigrasjonen_kommer_bare_indeksendringer</c>
/// fanget det. Regelen den håndhever er at byggemigrasjonen flyttes bakerst med nytt
/// tidsstempel — ikke at testen mykes opp.
///
/// Prisen er ett nytt bygg i hvert miljø som alt har kjørt denne. Det er bevisst: en
/// buffer som bygges for mye er billigere enn en migreringskjede som stopper på en tom
/// database. Se den nye migrasjonen for kjøretid og detaljer.
/// </summary>
public partial class RebuildCountCaches : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // TØMT, som 20261007134334_BuildCountCaches før den, og av samme grunn.
        //
        // 20261116135633_AddFacetCountCacheTables ligger etter denne og oppretter
        // tabeller. En migrasjonskropp er frosset på tidsstempelet sitt, men
        // skriptene den leser vedlikeholdes videre — så en skjemaendring bak
        // byggemigrasjonen er nøyaktig feilen som en gang stoppet en tom database
        // midt i migreringen. Byggingen er derfor flyttet til
        // 20261116143428_RebuildCountCachesWithFacets, som i tillegg bygger
        // fasett-bufferen.
        //
        // Skal bufferne bygges av en migrasjon, hører det i den nyeste
        // byggemigrasjonen — ikke her.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er avledede data, ikke skjema. Å tømme bufferne ved en
        // nedmigrering ville bare gjort spørringene tregere — tabellene slippes av
        // migrasjonene som opprettet dem.
    }
}
