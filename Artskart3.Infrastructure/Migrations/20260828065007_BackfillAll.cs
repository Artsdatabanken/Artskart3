using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Samlet datafylling — kjører Scripts/BackfillAll.sql, som er bygget inn som
/// ressurs slik at migrasjonen og den manuelle kjøringen er nøyaktig samme SQL.
///
/// Fyller, i rekkefølge:
///   A  rader i ObservationEntityIndex (flyttet hit fra migrasjon 20260608073333)
///   B  ObservationTaxonHierarchy
///   C  denormaliserte filterkolonner på ObservationEntityIndex
///   D  taksonrangkolonner på ObservationEntityIndex
///   E  CompleteFilter-kolonnene og ObservationDataset
///
/// HVORFOR ALT I ÉN MIGRASJON:
/// Rekkefølgen mellom disse er ikke valgfri — D leser det B skriver, E3 leser
/// det E1 skriver, og C ser etter markøren A legger igjen. Ligger de i hver sin
/// migrasjon, eller i fire manuelle skript, er rekkefølgen noe et menneske må
/// huske. Her er den kodet.
///
/// COLUMNSTORE BYGGES ÉN GANG, HER.
/// Skriptet slipper IX_OEI_Columnstore før datafyllingen og oppretter den etterpå
/// med full kolonneliste, inkludert CompleteFilter-kolonnene. Alternativet — å
/// deaktivere den her og utvide den i en egen migrasjon — ga to fulle bygg à
/// 10-30 minutter, det ene av dem mot tomme kolonner. Det er også grunnen til at
/// det ikke finnes noen egen «utvid columnstore»-migrasjon.
///
/// suppressTransaction ER AVGJØRENDE, ikke en optimalisering.
/// Uten den ligger hele kjøringen i én transaksjon: en timeout ruller da tilbake
/// alt arbeidet, og migrasjonen konvergerer aldri uansett hvor mange ganger den
/// kjøres. Med den committer hver batch for seg, loggen avkortes fortløpende, og
/// en avbrutt kjøring beholder det den rakk.
///
/// FORVENTET Å FEILE FLERE GANGER I PRODUKSJON — det er designet.
/// Skriptet fører vannmerke per seksjon i dbo.BackfillProgress, så hvert forsøk
/// starter der forrige stoppet i stedet for å skanne seg gjennom ferdig arbeid.
/// EF skriver ikke historikkraden før hele migrasjonen lykkes, så en feilet
/// kjøring blir liggende som ventende og kjøres igjen ved neste deploy. Etter
/// nok forsøk er den ferdig — og først da regnes den som anvendt.
///
/// Skriptet avslutter med en verifisering som hever feil hvis noe gjenstår.
/// Migrasjonen kan altså ikke bli stående som «anvendt» med ufullstendige data.
///
/// I MILJØER DER BACKFILLENE ER KJØRT MANUELT (test):
/// Hver seksjon finner ingenting å gjøre og faller igjennom. Kjøringen koster én
/// gjennomgang pluss ett columnstore-bygg, og ender likt med produksjon.
///
/// PÅ EN TOM DATABASE (fersk lokal utvikling):
/// Datafyllingen hopper over seg selv, men indeksene opprettes likevel. Ingen
/// feil, ingen gjentatte forsøk ved hver oppstart.
///
/// KJØRETID: timer i et tomt produksjonsmiljø. Krever hevet CommandTimeout i
/// ArtskartDbContextFactory og hevet timeoutInMinutes på pipeline-jobben.
/// </summary>
public partial class BackfillAll : Migration
{
    private const string ScriptResource = "Artskart3.Infrastructure.Scripts.BackfillAll.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ReadScript(), suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Dette er datafylling, ikke skjema — det finnes ingen
        // meningsfull vei tilbake, og å tømme tabellene ved en nedmigrering ville
        // vært langt verre enn å la dataene stå.
    }

    private static string ReadScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException(
                $"Fant ikke den innebygde ressursen '{ScriptResource}'. " +
                "Er EmbeddedResource-oppføringen for Scripts/BackfillAll.sql fjernet fra csproj-en?");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
