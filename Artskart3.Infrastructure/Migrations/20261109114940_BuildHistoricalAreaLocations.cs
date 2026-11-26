using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller HistoricalAreaLocation med Scripts/BuildHistoricalAreaLocations.sql, lest som innebygd ressurs.
///
/// Egen migrasjon fordi skriptet kjører uten transaksjon: i AddHistoricalAreaLocation ville tabellen
/// vært committet før skriptet startet, og et avbrutt bygg ville stoppet neste forsøk på CREATE TABLE.
/// Alene kan den kjøres på nytt, siden skriptet hopper over områder som allerede har rader.
///
/// Tidsstempelet er satt for hånd, så den ligger foran 20261116152626_RebuildCountCaches.
/// Målt lokalt til 36 minutter for 12,6 millioner rader, én gang per miljø.
/// </summary>
public partial class BuildHistoricalAreaLocations : Migration
{
    private const string Script = "Artskart3.Infrastructure.Scripts.BuildHistoricalAreaLocations.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ReadScript(), suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom: radene forsvinner med tabellen når AddHistoricalAreaLocation rulles tilbake.
    }

    private static string ReadScript()
    {
        using var stream = typeof(BuildHistoricalAreaLocations).Assembly.GetManifestResourceStream(Script)
            ?? throw new InvalidOperationException(
                $"Fant ikke den innebygde ressursen '{Script}'. " +
                "Er EmbeddedResource-oppføringen for skriptet fjernet fra csproj-en?");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
