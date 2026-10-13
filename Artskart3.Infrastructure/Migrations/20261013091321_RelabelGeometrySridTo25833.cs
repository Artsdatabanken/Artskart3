using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Merker alle geometrier om fra EPSG:32633 til EPSG:25833 — kjører
/// Scripts/RelabelGeometrySrid.sql, som er bygget inn som ressurs slik at
/// migrasjonen og den manuelle kjøringen er nøyaktig samme SQL.
///
/// HVORFOR: SQL Server reprojiserer aldri for geometry-typen. SRID er bare en
/// merkelapp, og en romlig operasjon mellom to ulike SRID-er gir NULL — ikke en
/// feilmelding. Location, Area og SensitiveObservationData er merket 32633,
/// maskeringsrutene 25833, og enhver sammenligning mellom de to settene har
/// derfor sett ut som «ingen overlapp». 25833 er valgt fordi spatial_ref_sys,
/// geometry_columns, API, frontend og SearchConstants.DefaultEpsgCode alt
/// bruker den; det er bare lagringen som har avveket.
///
/// OMMERKING, IKKE TRANSFORMASJON — godkjent av prosjektgruppen. Koordinatene
/// står urørt, så LocationAreas, områdetilhørighet og alle avledede tabeller er
/// upåvirket og skal ikke bygges om.
///
/// suppressTransaction ER AVGJØRENDE, ikke en optimalisering. Location har
/// 5,1 M geometrier. I én transaksjon ville en timeout rullet hele kjøringen
/// tilbake, og migrasjonen aldri konvergert. Med den committer hver batch for
/// seg, og en avbrutt kjøring beholder det den rakk.
///
/// KAN KJØRES FLERE GANGER. Skriptet fører vannmerke i dbo.BackfillProgress og
/// filtrerer på STSrid = 32633, så et nytt forsøk gjør bare det som gjenstår.
/// Det avslutter med en verifisering som hever feil hvis noe står igjen —
/// migrasjonen kan altså ikke bli stående som «anvendt» med en halvveis
/// ommerket base.
///
/// KJØRETID: titalls minutter i et produksjonslikt miljø. Krever hevet
/// CommandTimeout i ArtskartDbContextFactory (satt til 6 timer) og hevet
/// timeoutInMinutes på pipeline-jobben.
/// </summary>
public partial class RelabelGeometrySridTo25833 : Migration
{
    private const string ScriptResource = "Artskart3.Infrastructure.Scripts.RelabelGeometrySrid.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ReadScript(), suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. En nedmigrering ville merket geometriene tilbake til
        // 32633 — men importen og alt annet som skriver etter denne
        // migrasjonen merker 25833, så tilbakemerkingen ville rammet rader som
        // aldri var 32633. Ingenting krever den heller: koden som rulles
        // tilbake sammen med migrasjonen filtrerer på East/North og bryr seg
        // ikke om SRID.
    }

    private static string ReadScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException(
                $"Fant ikke den innebygde ressursen '{ScriptResource}'. " +
                "Er EmbeddedResource-oppføringen for Scripts/RelabelGeometrySrid.sql fjernet fra csproj-en?");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
