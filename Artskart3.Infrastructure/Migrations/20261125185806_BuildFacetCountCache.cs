using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Førstegangsbygget av fasett-bufferen — kjører Scripts/BuildFacetCountCache.sql,
/// lest som innebygd ressurs.
///
/// HVORFOR DEN LIGGER I EN EGEN MIGRASJON OG IKKE I DEVELOPS
/// Fasett-skriptet lå først i 20261117151539_RebuildCountCachesAfterTaxonRanks, som
/// bygget alle tre bufferne. Ved sammenslåingen med develop kom
/// 20261118095554_UpdateMdirWmsAdresses og 20261118131407_RebuildCountCaches bak den,
/// og vakten <c>Etter_byggemigrasjonen_kommer_bare_indeksendringer</c> slo ut.
///
/// Vaktens vanlige svar er å flytte byggingen inn i den nyeste byggemigrasjonen. Det
/// ble FORSØKT, og det var feil: 20261118131407 tilhører develop og er alt anvendt i
/// miljøene. EF har en historikkrad for den og kjører den aldri igjen, så et
/// fasett-skript lagt i kroppen hennes ville bare kjørt på ferske databaser. I alle
/// eksisterende miljøer hadde bufferen stått ubygget — uten at noe feilet, bare stille
/// tregt. En migrasjonskropp kan ikke endres etter at den er deployert.
///
/// Derfor denne: ny migrasjon, nyeste tidsstempel, bygger bare det develops ikke
/// bygger. Da stemmer begge tilfellene:
///
///   fersk database   ... → 20261118131407 (område + lokasjon) → denne (fasett)
///   migrert miljø    20261118131407 alt anvendt → denne kjører som ventende
///
/// REKKEFØLGEN ER ET KRAV
/// Skriptet slår opp flerverdi-bøttene (dimensjon 12 geografi, 13 prosjekt) i
/// AreaCountCacheBucketMember, som områdebufferen eier, og stopper med en tydelig feil
/// hvis den er tom. Områdebufferen bygges av 20261118131407, som ligger foran denne i
/// begge tilfellene over. Fasett-tabellene kommer fra
/// 20261116135633_AddFacetCountCacheTables, også foran.
///
/// BUFFERNE MÅ UANSETT BYGGES OM ETTER SAMMENSLÅINGEN
/// HiddenByAnnotation ble maskens sjette bit, og RowLayoutVersion gikk fra 2 til 3. I
/// en buffer bygget før det er biten alltid 0, så skjulte funn ville blitt talt med i
/// standardfilteret. Område- og lokasjonsbufferen dekkes av develops migrasjon, som
/// kom med samme endring; fasett-bufferen dekkes av denne.
///
/// KJØRETID: under fire minutter, målt 23.10.2026 over 134 705 341 indeksrader.
/// Krever hevet CommandTimeout (6 timer i ArtskartDbContextFactory) og at
/// pipeline-jobben har tilsvarende timeoutInMinutes.
///
/// SENERE VEDLIKEHOLD EIES AV JOBBENE, IKKE AV MIGRASJONER
/// Dette er førstegangsbygget. Ombygging etter import hører til ImportJob, og den
/// daglige ombyggingen til DatabaseMaintenanceJob. Når de finnes, skal ikke denne
/// migrasjonen utvides.
///
/// AVBRUDD ER TRYGT
/// Skriptet setter Status = 'Building' før det tømmer og bygger. Blir kjøringen
/// avbrutt, står bufferen igjen tom og ikke-klar, og fasett-tallene aggregeres som før
/// — riktige svar, bare tregere. EF registrerer ikke en migrasjon som feilet, så neste
/// forsøk bygger på nytt fra scratch.
///
/// suppressTransaction: skriptet tømmer med TRUNCATE og setter inn mange millioner
/// rader. Alt i én transaksjon ville sprengt loggen, og en feil ville gitt en like lang
/// rollback. MERK at TRUNCATE krever ALTER-rettighet på tabellen, ikke bare DELETE.
/// </summary>
public partial class BuildFacetCountCache : Migration
{
    private const string FacetScript = "Artskart3.Infrastructure.Scripts.BuildFacetCountCache.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(ReadScript(FacetScript), suppressTransaction: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er avledede data, ikke skjema. Å tømme bufferen ved en
        // nedmigrering ville bare gjort fasett-tallene tregere — tabellene slippes av
        // 20261116135633_AddFacetCountCacheTables, som opprettet dem.
    }

    private static string ReadScript(string resource)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Fant ikke den innebygde ressursen '{resource}'. " +
                "Er EmbeddedResource-oppføringen for skriptet fjernet fra csproj-en?");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
