using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Førstegangsbygget av begge tellebufferne — kjører Scripts/BuildAreaCountCache.sql
/// og Scripts/BuildLocationCountCache.sql, lest som innebygde ressurser.
///
/// HVORFOR DEN LIGGER HER OG IKKE I 20261007134334_BuildCountCaches
/// Den gjorde det, og det var feil. En migrasjonskropp må være frosset på
/// tidsstempelet sitt, men skriptene vedlikeholdes videre sammen med skjemaet. De
/// vokste til å referere Mask, UncertainIdentification, Introduced og
/// Min/MaxRegistrationStatusId — kolonner opprettet av migrasjoner ETTER 07.10 — og
/// en tom database stoppet derfor midt i migreringen med «Invalid column name
/// 'Mask'». Ingenting etter den ble anvendt. Se den migrasjonen for detaljene.
///
/// DENNE MÅ FORTSATT VÆRE DEN SISTE MIGRASJONEN
/// Flyttingen løser problemet bare så lenge alle kolonnene skriptene bruker finnes
/// når denne kjører. Legger noen til en kolonne i skriptene og en migrasjon for den,
/// er denne ikke lenger sist, og NØYAKTIG samme feil er tilbake.
///
/// Derfor finnes <c>Byggemigrasjonen_er_den_siste</c> i Artskart3.Tests.Unit: den
/// feiler hvis en migrasjon får et senere tidsstempel enn denne. Da skal denne
/// migrasjonen flyttes bakerst på nytt (nytt tidsstempel), ikke testen endres.
///
/// 20261021125247_AddFilterMaskToCountCaches tømmer bufferne og bytter
/// primærnøklene. Denne fyller dem igjen, etter at 20261022115207 har lagt til de
/// siste kolonnene. Uten dette steget står et nyoppsatt eller nettopp migrert miljø
/// med tomme buffere og Status = 'Building', og hvert område- og lokasjonskall
/// aggregerer live: riktige svar, men målt 2119 ms mot 84 ms ufiltrert.
///
/// REKKEFØLGEN ER ET KRAV, IKKE EN PREFERANSE
/// Lokasjonsbufferen slår opp flerverdi-bøtter (dimensjon 12 geografi, 13 prosjekt) i
/// AreaCountCacheBucketMember, som områdebufferen eier. Bygges den ikke først,
/// stopper det andre skriptet med en tydelig feil — og det er riktig, for
/// alternativet var en buffer der et fylkesvalg stille ga et tomt kart. Derfor ligger
/// begge i samme migrasjon, i rekkefølge: da kan de ikke kjøres i feil orden.
///
/// KJØRETID: målt 23.10.2026 til 827 s for områdebufferen og 632 s for
/// lokasjonsbufferen over 134 705 341 indeksrader — omtrent 24 minutter til sammen.
/// Betales én gang per miljø; migrasjonen registreres og kjører ikke igjen. I et lite
/// miljø går den på sekunder. Krever hevet CommandTimeout (6 timer i
/// ArtskartDbContextFactory) og at pipeline-jobben har tilsvarende timeoutInMinutes.
///
/// SENERE VEDLIKEHOLD EIES AV JOBBENE, IKKE AV MIGRASJONER
/// Dette er førstegangsbygget. Ombygging etter import hører til ImportJob, og den
/// daglige ombyggingen til DatabaseMaintenanceJob — se MERK i begge. Når de finnes,
/// skal ikke denne migrasjonen utvides.
///
/// AVBRUDD ER TRYGT
/// Begge skriptene setter Status = 'Building' før de tømmer og bygger. Blir kjøringen
/// avbrutt, står bufferen igjen tom og ikke-klar, og oppslaget aggregerer som før —
/// riktige svar, bare tregere. EF registrerer ikke en migrasjon som feilet, så neste
/// forsøk bygger på nytt fra scratch. Det er nettopp derfor et skript uten resumering
/// er forsvarlig her.
///
/// suppressTransaction: skriptene tømmer med TRUNCATE og setter inn rundt 74
/// millioner rader i lokasjonsbufferen alene. Alt i én transaksjon ville sprengt
/// loggen, og en feil ville gitt en like lang rollback. MERK at TRUNCATE krever
/// ALTER-rettighet på tabellen, ikke bare DELETE.
///
/// BEGGE SKRIPTENE KJØRER PÅ SAMME TILKOBLING
/// EF sender dem som to kommandoer over én connection, og #temp-tabeller lever ut
/// SESJONEN — ikke batchen. Skriptene deler sju temptabellnavn, så begge rydder sine
/// egne temptabeller først.
/// </summary>
public partial class RebuildCountCaches : Migration
{
    private const string AreaScript = "Artskart3.Infrastructure.Scripts.BuildAreaCountCache.sql";
    private const string LocationScript = "Artskart3.Infrastructure.Scripts.BuildLocationCountCache.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Områdebufferen først — den eier AreaCountCacheBucketMember.
        migrationBuilder.Sql(ReadScript(AreaScript), suppressTransaction: true);
        migrationBuilder.Sql(ReadScript(LocationScript), suppressTransaction: true);
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
