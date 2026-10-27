using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Sjekker kolonnekartet i <see cref="ImportStagingSchema"/> mot det skjemaet
/// databasen faktisk har: finnes kolonnen, er typen den samme, og er den lang nok.
///
/// HVORFOR DET ER VERDT EN EGEN TEST: et feilskrevet kolonnenavn oppdages ellers først
/// når publiseringen kjører mot en ekte base, og da som en SQL-feil midt i en import.
/// Dette er den samme sjekken, men på et sekund og uten å skrive noe.
///
/// AVSLÅTT SOM STANDARD:
///
///   $env:ARTSKART_INDEX_CONNECTION = "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~ImportStagingSchemaParity"
///
/// Trenger ikke Docker. Kun lesing.
/// </summary>
public class ImportStagingSchemaParityTests
{
    private const string ConnectionVariable = "ARTSKART_INDEX_CONNECTION";

    private readonly ITestOutputHelper _output;

    public ImportStagingSchemaParityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Staging-kolonner som ikke finnes i måltabellen fordi publiseringen gjør dem om:
    /// forretningsnøkler blir Id-er, og WKB blir geometri.
    /// </summary>
    private static readonly HashSet<string> OversettesIPubliseringen =
    [
        "ProxyId",        // -> ObservationId, eller Id på detaljraden
        "LinkedProxyId",  // -> LinkedObservationId
        "LookupId",       // -> LocationId
        "GeometryWkb"     // -> Geometry
    ];

    [Fact]
    public void Alle_kolonner_finnes_i_maltabellen_med_samme_type()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var skjema = LesSkjema(connectionString);
        var avvik = new List<string>();
        var sjekket = 0;

        foreach (var tabell in ImportStagingSchema.All)
        {
            foreach (var kolonne in tabell.Columns)
            {
                // ProxyId på Observation ER en ekte kolonne; det er bare på barna den
                // oversettes. Samme for LookupId på Location.
                var egenKolonne = tabell.TargetTable is "Observation" && kolonne.Name is "ProxyId"
                                  || tabell.TargetTable is "Location" && kolonne.Name is "LookupId";

                if (!egenKolonne && OversettesIPubliseringen.Contains(kolonne.Name)) continue;

                sjekket++;

                if (!skjema.TryGetValue((tabell.TargetTable, kolonne.Name), out var faktisk))
                {
                    avvik.Add($"{tabell.TargetTable}.{kolonne.Name} finnes ikke i databasen");
                    continue;
                }

                if (!string.Equals(faktisk.Type, kolonne.BaseType, StringComparison.OrdinalIgnoreCase))
                {
                    avvik.Add($"{tabell.TargetTable}.{kolonne.Name} er {faktisk.Type} i databasen, "
                              + $"men {kolonne.BaseType} i kartet");
                    continue;
                }

                var vårLengde = Lengde(kolonne.SqlType);

                if (vårLengde is not null && faktisk.Length is not null && vårLengde > faktisk.Length)
                {
                    avvik.Add($"{tabell.TargetTable}.{kolonne.Name} er {faktisk.Length} tegn i databasen, "
                              + $"men {vårLengde} i kartet");
                }
            }
        }

        _output.WriteLine($"Sjekket {sjekket} kolonner i {ImportStagingSchema.All.Count} tabeller");

        foreach (var linje in avvik) _output.WriteLine("  avvik: " + linje);

        avvik.Should().BeEmpty();
    }

    /// <summary>
    /// Ingen av kolonnene i kartet skal være beregnet av databasen — en innsetting i en
    /// beregnet kolonne feiler.
    /// </summary>
    [Fact]
    public void Ingen_kolonne_er_beregnet_av_databasen()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var beregnede = LesBeregnede(connectionString);

        var treff = ImportStagingSchema.All
            .SelectMany(t => t.Columns.Select(c => (t.TargetTable, c.Name)))
            .Where(x => beregnede.Contains(x))
            .ToList();

        _output.WriteLine($"{beregnede.Count} beregnede kolonner i basen");

        treff.Should().BeEmpty();
    }

    private static Dictionary<(string Tabell, string Kolonne), (string Type, int? Length)> LesSkjema(
        string connectionString)
    {
        const string sql = """
            SELECT t.name AS TabellNavn, c.name AS KolonneNavn, ty.name AS TypeNavn, c.max_length
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            """;

        var resultat = new Dictionary<(string, string), (string, int?)>();

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(sql, connection);
        using var leser = command.ExecuteReader();

        while (leser.Read())
        {
            var type = leser.GetString(2);
            var maks = leser.GetInt16(3);

            int? lengde = type is "nvarchar" or "nchar"
                ? maks == -1 ? null : maks / 2
                : null;

            resultat[(leser.GetString(0), leser.GetString(1))] = (type, lengde);
        }

        return resultat;
    }

    private static HashSet<(string, string)> LesBeregnede(string connectionString)
    {
        const string sql = """
            SELECT t.name, c.name
            FROM sys.computed_columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            """;

        var resultat = new HashSet<(string, string)>();

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(sql, connection);
        using var leser = command.ExecuteReader();

        while (leser.Read()) resultat.Add((leser.GetString(0), leser.GetString(1)));

        return resultat;
    }

    /// <summary>Lengden i en SQL-type, eller <c>null</c> for <c>max</c> og typer uten.</summary>
    private static int? Lengde(string sqlType)
    {
        var start = sqlType.IndexOf('(', StringComparison.Ordinal);

        if (start < 0) return null;

        var innhold = sqlType[(start + 1)..^1];

        return int.TryParse(innhold, out var lengde) ? lengde : null;
    }
}
