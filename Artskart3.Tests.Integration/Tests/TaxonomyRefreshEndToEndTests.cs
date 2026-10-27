extern alias workers;

using Artskart3.Core.Application.Taxonomy;
using Artskart3.Core.Application.Persistence;
using Artskart3.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using RavenTaxonomySource = workers::Artskart3.Workers.Taxonomy.RavenTaxonomySource;
using TaxonomyOptions = workers::Artskart3.Workers.Taxonomy.TaxonomyOptions;
using TaxonomyRefreshService = workers::Artskart3.Workers.Taxonomy.TaxonomyRefreshService;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Speiler taksonomien fra Artskart 2s RavenDB inn i en Artskart 3-base, og måler
/// resultatet mot Artskart 2s fasit.
///
/// DETTE ER VERIFISERINGEN AV PORTEN. Artskart 2s gullbaser fikk taksonomien sin fra de
/// samme dokumentene; stemmer tabellene etterpå, har speilingen gjort samme jobb som
/// <c>TaxonImporter</c>.
///
/// KREVER RAVENDB OPPE:
///
///   RavenLocal.exe --data "C:\RavenData-frossen" --host-only
///
/// DEN FROSNE KOPIEN, ikke den i drift — Artskart 2s import SKRIVER til RavenDB.
///
///   $env:ARTSKART_GULLSETT_CONNECTION = "...initial catalog=Artskart3Index_GullsettMedium..."
///   $env:ARTSKART_RAVEN_ADDRESS       = "http://localhost:8080/"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~TaxonomyRefreshEndToEnd"
///
/// SAMME GJERDE SOM GULLSETT-TESTEN: markørtabellen <c>__Gullsett</c>. Speilingen
/// skriver til taksonomitabellene, og de skal ikke røres i en base som ikke er merket.
/// </summary>
public class TaxonomyRefreshEndToEndTests
{
    private readonly ITestOutputHelper _output;

    public TaxonomyRefreshEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Speiler_taksonomien_fra_ravendb()
    {
        var gullbase = Environment.GetEnvironmentVariable("ARTSKART_GULLSETT_CONNECTION");
        var raven = Environment.GetEnvironmentVariable("ARTSKART_RAVEN_ADDRESS");

        if (string.IsNullOrWhiteSpace(gullbase) || string.IsNullOrWhiteSpace(raven))
        {
            _output.WriteLine("ARTSKART_GULLSETT_CONNECTION og ARTSKART_RAVEN_ADDRESS må være satt "
                              + "- testen gjør ingenting.");
            return;
        }

        await KrevGullbase(gullbase);

        var før = await LesAntall(gullbase);

        await using var leverandør = Bygg(gullbase, raven);
        using var scope = leverandør.CreateScope();

        var tjeneste = scope.ServiceProvider.GetRequiredService<TaxonomyRefreshService>();
        var resultat = await tjeneste.RefreshAsync(CancellationToken.None);

        var etter = await LesAntall(gullbase);

        _output.WriteLine($"=== Speiling fra {resultat.Source} ===");
        _output.WriteLine($"Ranger {resultat.Ranks}, grupper {resultat.Groups}, "
                          + $"taksoner {resultat.Taxa}, navn {resultat.Names}, "
                          + $"populærnavn {resultat.PopularNames}, egenskaper {resultat.Properties}");
        _output.WriteLine($"Tid: {resultat.Elapsed.TotalSeconds:F1} s");
        _output.WriteLine(string.Empty);
        _output.WriteLine("Tabell                 før      etter");

        foreach (var (tabell, antall) in etter)
        {
            _output.WriteLine($"{tabell,-20} {før[tabell],8} {antall,10}");
        }

        resultat.Taxa.Should().BeGreaterThan(100_000, "RavenDB-kopien har over 180 000 taksoner");
        resultat.Names.Should().BeGreaterThan(100_000);
        etter["Taxon"].Should().BeGreaterThanOrEqualTo(før["Taxon"], "speilingen skal aldri slette");
        etter["TaxonName"].Should().BeGreaterThanOrEqualTo(før["TaxonName"]);
    }

    private static async Task KrevGullbase(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE name = '__Gullsett';", connection);

        var markør = (int)(await command.ExecuteScalarAsync())!;

        markør.Should().Be(1, "speilingen skriver, og skal bare kjøre mot en merket gullbase");
    }

    private static ServiceProvider Bygg(string gullbase, string ravenAdresse)
    {
        var services = new ServiceCollection();

        services.AddLogging(x => x.SetMinimumLevel(LogLevel.Information).AddSimpleConsole());

        services.AddSingleton(new TaxonomyOptions { BaseAddress = ravenAdresse });

        services.AddHttpClient<ITaxonomySource, RavenTaxonomySource>((sp, klient) =>
        {
            klient.BaseAddress = new Uri(sp.GetRequiredService<TaxonomyOptions>().BaseAddress);
            klient.Timeout = TimeSpan.FromMinutes(30);
        });

        services.AddDbContext<ArtskartDbContext>(options =>
        {
            options.UseSqlServer(gullbase, sql =>
            {
                sql.UseNetTopologySuite();
                sql.CommandTimeout(1800);
            });

            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IArtsKartDbContext>(sp => sp.GetRequiredService<ArtskartDbContext>());
        services.AddScoped<TaxonomyRefreshService>();

        return services.BuildServiceProvider();
    }

    private static async Task<Dictionary<string, int>> LesAntall(string connectionString)
    {
        string[] tabeller =
            ["TaxonRank", "TaxonGroup", "Taxon", "TaxonName", "TaxonPopularName", "TaxonProperty"];

        var resultat = new Dictionary<string, int>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in tabeller)
        {
            await using var command = new SqlCommand(
                $"SELECT COUNT(*) FROM dbo.{tabell};", connection) { CommandTimeout = 600 };

            resultat[tabell] = (int)(await command.ExecuteScalarAsync())!;
        }

        return resultat;
    }
}
