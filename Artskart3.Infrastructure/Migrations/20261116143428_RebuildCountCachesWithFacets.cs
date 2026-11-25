
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// BYGGER IKKE LENGER NOE. BYGGINGEN LIGGER I
/// 20261117151539_RebuildCountCachesAfterTaxonRanks.
///
/// Den kjørte Scripts/BuildAreaCountCache.sql, BuildLocationCountCache.sql og
/// BuildFacetCountCache.sql, og var selv flyttingen bort fra
/// 20261111112446_RebuildCountCaches. Nå er den flyttet videre, fordi
/// 20261117151112_AddAllTaxonRankColumnsToObservationEntityIndex la seg bak den og
/// legger til kolonner. Samme regel, tredje gang — se den nye migrasjonen og
/// <c>CountCacheBuildMigrationTests</c>.
///
/// Historikken under står igjen fordi den forklarer HVORFOR regelen finnes.
///
/// HVORFOR DEN ERSTATTET 20261111112446_RebuildCountCaches
/// Fasett-bufferen fikk sine tabeller i 20261116135633_AddFacetCountCacheTables, som
/// ligger ETTER den forrige byggemigrasjonen. En migrasjonskropp er frosset på
/// tidsstempelet sitt, men skriptene den leser vedlikeholdes videre — så en
/// skjemaendring bak byggemigrasjonen er nøyaktig den feilen som en gang gjorde at en
/// tom database stoppet midt i migreringen med «Invalid column name 'Mask'». Se
/// 20261007134334_BuildCountCaches.
///
/// Reglen er dokumentert i <c>CountCacheBuildMigrationTests</c>: kommer det en
/// migrasjon etter byggemigrasjonen som gjør mer enn å endre indekser, skal
/// byggemigrasjonen flyttes bakerst med nytt tidsstempel — ikke testen endres. Det er
/// det denne er. Den forrige står igjen tom, som den før den.
///
/// PRISEN ER ETT NYTT BYGG PER MILJØ
/// Et miljø som alt har kjørt 20261111112446 ser dette som en ny migrasjon og bygger
/// område- og lokasjonsbufferen om, selv om ingenting de trenger er endret. Målt
/// 23.10.2026 til 827 s + 632 s, altså ~24 minutter, pluss ~4 minutter for
/// fasett-bufferen. Alternativet var å gjøre vakten i testen tabellbevisst, altså
/// svakere, for å spare en engangskostnad — og en buffer som bygges for mye er
/// billigere enn en migreringskjede som stopper på en tom database.
///
/// REKKEFØLGEN ER ET KRAV, IKKE EN PREFERANSE
/// Både lokasjons- og fasett-bufferen slår opp flerverdi-bøtter (dimensjon 12
/// geografi, 13 prosjekt) i AreaCountCacheBucketMember, som områdebufferen eier.
/// Bygges den ikke først, stopper de to andre skriptene med en tydelig feil — og det
/// er riktig, for alternativet var buffere der et fylkesvalg stille ga tomt svar.
/// Fasett-skriptet sjekker i tillegg at hver områdemengde i indekstabellen finnes som
/// bøtte, så en medlemstabell fra et eldre datasett stopper bygget framfor å gi for
/// lave fasett-tall. Derfor ligger alle tre i samme migrasjon, i rekkefølge: da kan de
/// ikke kjøres i feil orden.
///
/// KJØRETID: ~24 minutter for de to første (målt 23.10.2026 over 134 705 341
/// indeksrader) og under fire minutter for fasett-bufferen. Betales én gang per miljø.
/// Krever hevet CommandTimeout (6 timer i ArtskartDbContextFactory) og at
/// pipeline-jobben har tilsvarende timeoutInMinutes.
///
/// SENERE VEDLIKEHOLD EIES AV JOBBENE, IKKE AV MIGRASJONER
/// Dette er førstegangsbygget. Ombygging etter import hører til ImportJob, og den
/// daglige ombyggingen til DatabaseMaintenanceJob — se MERK i begge. Når de finnes,
/// skal ikke denne migrasjonen utvides.
///
/// AVBRUDD ER TRYGT
/// Alle tre skriptene setter Status = 'Building' før de tømmer og bygger. Blir
/// kjøringen avbrutt, står bufferen igjen tom og ikke-klar, og oppslaget aggregerer
/// som før — riktige svar, bare tregere. EF registrerer ikke en migrasjon som feilet,
/// så neste forsøk bygger på nytt fra scratch.
///
/// suppressTransaction: skriptene tømmer med TRUNCATE og setter inn rundt 74
/// millioner rader i lokasjonsbufferen alene. Alt i én transaksjon ville sprengt
/// loggen, og en feil ville gitt en like lang rollback. MERK at TRUNCATE krever
/// ALTER-rettighet på tabellen, ikke bare DELETE.
///
/// ALLE SKRIPTENE KJØRER PÅ SAMME TILKOBLING
/// EF sender dem som kommandoer over én connection, og #temp-tabeller lever ut
/// SESJONEN — ikke batchen. Skriptene deler flere temptabellnavn, så hvert av dem
/// rydder sine egne først.
/// </summary>
public partial class RebuildCountCachesWithFacets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // TØMT, som de to byggemigrasjonene før den, og av samme grunn.
        //
        // 20261117151112_AddAllTaxonRankColumnsToObservationEntityIndex la seg bak
        // denne og legger til kolonner. Byggingen er derfor flyttet til
        // 20261117151539_RebuildCountCachesAfterTaxonRanks.
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
