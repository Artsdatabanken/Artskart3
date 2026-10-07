using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Bygger områdebufferen og lokasjonsbufferen — kjører Scripts/BuildAreaCountCache.sql
/// og Scripts/BuildLocationCountCache.sql, som er bygget inn som ressurser.
///
/// HVORFOR SOM MIGRASJON
/// Bufferne betjener de tregeste spørringene vi har: det ufiltrerte lokasjonsoppslaget
/// lå rundt to sekunder for Oslo-utsnittet før bufferen. Tabellene opprettes av
/// migrasjoner, tjenestene leser dem, men ingenting fyller dem — så et nytt miljø har
/// tomme buffere og aggregerer alt. Det virker (oppslaget faller tilbake til tellingen
/// når bufferen ikke er klar), men det er tregt, og forskjellen er hele poenget med
/// tabellene.
///
/// Samme form som BackfillAll: avledede data fylt av en migrasjon, og senere vedlikeholdt
/// av importjobben. Når den finnes, er det den som bygger bufferne på nytt etter hver
/// import — se MERK i ImportJob. Denne migrasjonen er førstegangsbygget.
///
/// REKKEFØLGEN ER ET KRAV, IKKE EN PREFERANSE
/// Lokasjonsbufferen slår opp flerverdi-bøtter (dimensjon 12 geografi, 13 prosjekt) i
/// AreaCountCacheBucketMember, som områdebufferen eier. Bygges den ikke først, stopper
/// det andre skriptet med en tydelig feil — og det er riktig, for alternativet var en
/// buffer der et fylkesvalg stille ga et tomt kart. Derfor ligger begge i samme
/// migrasjon, i rekkefølge: da kan de ikke kjøres i feil orden.
///
/// KJØRETID: målt 592 s for områdebufferen og 351 s for lokasjonsbufferen over
/// 134 705 341 indeksrader, altså omtrent 16 minutter til sammen. I et lite miljø går
/// den på sekunder — kostnaden følger datamengden, akkurat som gevinsten. Krever hevet
/// CommandTimeout (satt til 6 timer i ArtskartDbContextFactory) og at pipeline-jobben
/// har tilsvarende timeoutInMinutes.
///
/// AVBRUDD ER TRYGT
/// Begge skriptene setter Status = 'Building' før de tømmer og bygger. Blir kjøringen
/// avbrutt, står bufferen igjen tom og ikke-klar, og oppslaget aggregerer som før —
/// riktige svar, bare tregere. EF registrerer ikke en migrasjon som feilet, så neste
/// forsøk bygger på nytt fra scratch. Det er nettopp derfor et skript uten resumering
/// er forsvarlig her.
///
/// suppressTransaction: skriptene tømmer med TRUNCATE og setter inn rundt 75 millioner
/// rader i lokasjonsbufferen alene. Samme resonnement som i BackfillAll — alt i én
/// transaksjon ville sprengt loggen, og en feil ville gitt en like lang rollback.
/// MERK at TRUNCATE krever ALTER-rettighet på tabellen, ikke bare DELETE.
///
/// BEGGE SKRIPTENE KJØRER PÅ SAMME TILKOBLING
/// EF sender dem som to kommandoer over én connection, og #temp-tabeller lever ut
/// SESJONEN — ikke batchen. Skriptene deler sju temptabellnavn, og første forsøk
/// feilet derfor på «There is already an object named '#dim'» etter at områdebufferen
/// var ferdig bygget. Begge skriptene rydder nå sine egne temptabeller først.
///
/// Uten transaksjon er delvis anvendelse mulig, som advarselen fra EF sier: området
/// kan være bygget mens lokasjonen ikke er. Det er ufarlig her — en ikke-klar buffer
/// aggregeres bare rundt — og migrasjonen registreres ikke, så neste forsøk bygger
/// begge på nytt.
/// </summary>
public partial class BuildCountCaches : Migration
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
