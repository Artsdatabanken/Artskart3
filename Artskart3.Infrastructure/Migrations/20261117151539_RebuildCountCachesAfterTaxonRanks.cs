using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Førstegangsbygget av alle tre tellebufferne — kjører
/// Scripts/BuildAreaCountCache.sql, BuildLocationCountCache.sql og
/// BuildFacetCountCache.sql, lest som innebygde ressurser.
///
/// TREDJE FLYTTING AV SAMME MIGRASJON
/// 20261007134334_BuildCountCaches → 20261111112446_RebuildCountCaches →
/// 20261116143428_RebuildCountCachesWithFacets → hit. Hver gang av samme grunn: en
/// migrasjonskropp er frosset på tidsstempelet sitt, mens skriptene den leser
/// vedlikeholdes videre. Ligger en skjemaendring bak byggemigrasjonen, kan en tom
/// database stoppe midt i migreringen — det skjedde én gang, med «Invalid column
/// name 'Mask'». Se 20261007134334_BuildCountCaches.
///
/// Denne gangen var det 20261117151112_AddAllTaxonRankColumnsToObservationEntityIndex
/// som la seg bak.
///
/// FLYTTINGEN VAR IKKE STRENGT NØDVENDIG DENNE GANGEN
/// De tre byggeskriptene leser bare ÉN taksonkolonne, `OrderTaxonId`, og den fantes
/// fra før — de 19 nye rangkolonnene berører dem ikke. Flyttingen er altså ren
/// kostnad for korrektheten her: ett nytt bygg på ~28 minutter per miljø.
///
/// Den er gjort likevel, fordi <c>CountCacheBuildMigrationTests</c> er en bevisst
/// blunt vakt: den kan ikke se hva rå SQL gjør, og dens egen dokumentasjon sier at
/// byggemigrasjonen skal flyttes framfor at testen mykes opp. Å gjøre vakten
/// kolonnebevisst for å spare én ombygging ville gjort den svakere for den dagen det
/// faktisk gjelder. Prisen er kjent og betalt to ganger før.
///
/// REKKEFØLGEN ER ET KRAV, IKKE EN PREFERANSE
/// Både lokasjons- og fasett-bufferen slår opp flerverdi-bøtter i
/// AreaCountCacheBucketMember, som områdebufferen eier. Derfor ligger alle tre i
/// samme migrasjon, i rekkefølge.
///
/// FORUTSETTER AT BackfillAll HAR FYLT KOLONNENE
/// Skriptene bøtter på `OrderTaxonId`. Er den ikke fylt, havner alt i NULL-bøtta og
/// bufferen blir meningsløs. Det er sant også før denne endringen, men verdt å
/// gjenta: backfillen kommer før byggingen.
///
/// KJØRETID: ~24 minutter for område- og lokasjonsbufferen (målt 23.10.2026 over
/// 134 705 341 indeksrader) og under fire minutter for fasett-bufferen. Krever hevet
/// CommandTimeout (6 timer i ArtskartDbContextFactory).
///
/// AVBRUDD ER TRYGT. Alle tre skriptene setter Status = 'Building' før de tømmer, så
/// en avbrutt kjøring etterlater en ikke-klar buffer og oppslagene aggregerer som før.
/// </summary>
public partial class RebuildCountCachesAfterTaxonRanks : Migration
{
    private const string AreaScript = "Artskart3.Infrastructure.Scripts.BuildAreaCountCache.sql";
    private const string LocationScript = "Artskart3.Infrastructure.Scripts.BuildLocationCountCache.sql";
    private const string FacetScript = "Artskart3.Infrastructure.Scripts.BuildFacetCountCache.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Områdebufferen først — den eier AreaCountCacheBucketMember, som de to andre
        // leser flerverdi-bøttene fra.
        migrationBuilder.Sql(ReadScript(AreaScript), suppressTransaction: true);
        migrationBuilder.Sql(ReadScript(LocationScript), suppressTransaction: true);
        migrationBuilder.Sql(ReadScript(FacetScript), suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er avledede data, ikke skjema. Å tømme bufferne ved en
        // nedmigrering ville bare gjort spørringene tregere — tabellene slippes av
        // migrasjonene som opprettet dem.
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
