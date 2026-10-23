using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// BYGGER IKKE LENGER NOE. FØRSTEGANGSBYGGET AV BUFFERNE ER EN DRIFTSOPPGAVE.
///
/// Migrasjonen kjørte Scripts/BuildAreaCountCache.sql og BuildLocationCountCache.sql,
/// lest som innebygde ressurser. Det var feil, og det er rettet her.
///
/// HVORFOR DET VAR FEIL
/// En migrasjonskropp må være frosset på tidsstempelet sitt. Denne leste skriptfilene
/// slik de ser ut I DAG, og skriptene vedlikeholdes videre sammen med skjemaet. De
/// refererer nå kolonner som opprettes av SENERE migrasjoner:
///
///   Mask                                    20261021125247_AddFilterMaskToCountCaches
///   UncertainIdentification, Introduced     20261020131641_AddObservationIdentification…
///   Min/MaxRegistrationStatusId             20261022115207_AddRegistrationStatusTo…
///
/// På en tom database nådde «dotnet ef database update» hit og stoppet med
/// «Invalid column name 'Mask'». EF registrerer ikke en migrasjon som feilet, så
/// INGENTING etter denne ble anvendt heller. Eksisterende miljøer har allerede
/// historikkraden og merket det ikke — feilen traff bare nye miljøer, gjenoppbygde
/// testdatabaser og katastrofegjenoppretting. Integrasjonstestene fanget den ikke
/// fordi de bygger skjemaet med EnsureCreatedAsync fra modellen, som har alle
/// kolonnene.
///
/// HVORFOR VI IKKE BARE FRØS DEN GAMLE SQL-EN INN
/// Den ville bygget bufferne etter skjemaet fra 07.10 — og 20261021125247 TRUNKERER
/// alle tre buffertabellene to uker senere i samme kjøring. Et nytt miljø ville brukt
/// de målte 16 minuttene på rader som garantert ble slettet før migreringen var ferdig.
/// Migrasjonens formål kan ikke lenger oppfylles fra plasseringen den har.
///
/// HVORDAN BUFFERNE FYLLES NÅ
/// Kjør de to skriptene etter at migrasjonene er ferdige — områdebufferen FØRST:
///
///   sqlcmd -S {server} -d {database} -E -t 0 -i Scripts/BuildAreaCountCache.sql
///   sqlcmd -S {server} -d {database} -E -t 0 -i Scripts/BuildLocationCountCache.sql
///
/// REKKEFØLGEN ER ET KRAV, IKKE EN PREFERANSE. Lokasjonsbufferen slår opp
/// flerverdi-bøtter (dimensjon 12 geografi, 13 prosjekt) i AreaCountCacheBucketMember,
/// som områdebufferen eier. Kjøres den først, stopper skriptet med en tydelig feil —
/// og det er riktig, for alternativet var en buffer der et fylkesvalg stille ga et
/// tomt kart.
///
/// Målt 23.10.2026: 827 s for områdebufferen og 632 s for lokasjonsbufferen over
/// 134 705 341 indeksrader, altså omtrent 24 minutter til sammen. Krever hevet
/// kommandotimeout (-t 0 over sqlcmd).
///
/// AT DE ER TOMME ER TRYGT, BARE TREGT
/// Begge tjenestene avviser en buffer som ikke er «Ready» og aggregerer som før —
/// riktige svar, målbart tregere. Se nødbryterne i AreaCountCacheOptions og
/// LocationCountCacheOptions, og MERK i ImportJob om hvem som eier ombyggingen når
/// importen kommer.
/// </summary>
public partial class BuildCountCaches : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Se klassekommentaren: skriptene kan ikke spilles av herfra,
        // og rader bygget her ville blitt trunkert av 20261021125247 uansett.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er avledede data, ikke skjema. Å tømme bufferne ved en
        // nedmigrering ville bare gjort spørringene tregere — tabellene slippes av
        // migrasjonene som opprettet dem.
    }
}
