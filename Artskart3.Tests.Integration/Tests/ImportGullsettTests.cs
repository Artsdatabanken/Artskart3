extern alias workers;

using Artskart3.Core.Application.Import.Writing;
using Artskart3.Core.Application.Persistence;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.DependencyInjection;
using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using ImportJob = workers::Artskart3.Workers.Import.ImportJob;
using ImportService = workers::Artskart3.Workers.Import.ImportService;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kjører et gullsett inn i gullbasen, slik paritetsriggen skal gjøre det (plan 4.8.3).
///
/// FORSKJELLEN FRA <see cref="ImportNodeEndToEndTests"/> er at denne kjører FLERE noder
/// inn i SAMME base og <b>tømmer den ikke</b>. Et gullsett er summen av nodene sine, og
/// Artskart 2s fasit er bygget på samme vis — én base, alle nodene i settet. Rundturs-
/// testene tømmer med vilje, og det er riktig for dem; her ville det slettet settet.
///
/// KREVER:
///
///   $env:ARTSKART_CACHE_CONNECTION    = "...initial catalog=Artskart2Cache-2026-9-22-14-36..."
///   $env:ARTSKART_GULLSETT_CONNECTION = "...initial catalog=Artskart3Index_GullsettMedium..."
///   $env:ARTSKART_GULLSETT_NODES      = "135,194,43,39,59,101"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~ImportGullsett"
///
/// MINSTE NODE FØRST er verdt å holde på: en konfigurasjonsfeil viser seg da på sekunder
/// i stedet for etter at node 101 har brukt åtte minutter på å lese 1,85 millioner poster.
///
/// EGET GJERDE: markørtabellen <c>__Gullsett</c>. Gullbasen skal med vilje <i>ikke</i>
/// ha <c>__ImportWriteTest</c> — da kan ingen rundturstest tømme den, og denne testen
/// kan ikke treffe skrivetestbasen.
///
/// NODENE MÅ FINNES I <c>DataSource</c>, og basen må ha referansedata (områder,
/// taksonomi, organisasjoner, kategorier) og minst én rad i
/// <c>ProsessEngineHistory</c>. Registeret fylles ellers av speilingen fra Artskart 2
/// (plan 4.9.2, åpent spørsmål 2e).
///
/// NODE 39 GIR ET KJENT AVVIK. Artskart 2 maskerte 742 poster der, og maskeringen er
/// fase 8, som ikke finnes — så Artskart 3 skriver dem umaskert. Det er målt og
/// navngitt i paritetsrapporten, ikke en overraskelse, og cache-kopien er skrubbet, så
/// ingen ekte koordinat lekker. Vil man måle uten den støyen, er lite uten node 39
/// (<c>43,135,194</c>) settet som gir 0 avvik på maskering.
/// </summary>
public class ImportGullsettTests
{
    private readonly ITestOutputHelper _output;

    public ImportGullsettTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Importerer_gullsettet()
    {
        var cache = Environment.GetEnvironmentVariable("ARTSKART_CACHE_CONNECTION");
        var gullbase = Environment.GetEnvironmentVariable("ARTSKART_GULLSETT_CONNECTION");
        var noder = LesNoder(Environment.GetEnvironmentVariable("ARTSKART_GULLSETT_NODES"));

        if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(gullbase) || noder.Length == 0)
        {
            _output.WriteLine("ARTSKART_CACHE_CONNECTION, ARTSKART_GULLSETT_CONNECTION og "
                              + "ARTSKART_GULLSETT_NODES må være satt - testen gjør ingenting.");
            return;
        }

        await KrevGullbase(gullbase);

        // Tømming er opt-in. En kjøring uten den oppdaterer postene den ser igjen
        // (MERGE på ProxyId), men den fjerner ikke rader fra en tidligere kjøring som
        // nå blir avvist. Skal målingen være ren etter en kodeendring, sett flagget.
        if (Environment.GetEnvironmentVariable("ARTSKART_GULLSETT_RYDD") == "1")
        {
            await Rydd(gullbase);
            _output.WriteLine("Gullbasen er tømt før kjøringen.");
        }

        await using var leverandør = Bygg(cache, gullbase, noder);
        using var scope = leverandør.CreateScope();

        var tjeneste = scope.ServiceProvider.GetRequiredService<ImportService>();
        var resultater = await tjeneste.ImportActiveNodesAsync(CancellationToken.None);

        _output.WriteLine($"=== Gullsett: node {string.Join(", ", noder)} ===");
        _output.WriteLine("Node   Lest  Importert  Avvist  Slettet  Ulesbare  Lokaliteter  Runder   Sek");

        foreach (var r in resultater)
        {
            _output.WriteLine(
                $"{r.NodeId,4} {r.Read,6} {r.Imported,10} {r.Rejected,7} {r.Deleted,8} "
                + $"{r.Malformed,9} {r.Locations,12} {r.Rounds,7} {r.Elapsed.TotalSeconds,5:F1}");
        }

        _output.WriteLine(
            $"Sum  {resultater.Sum(x => x.Read),6} {resultater.Sum(x => x.Imported),10} "
            + $"{resultater.Sum(x => x.Rejected),7}");

        var skrevet = await LesAntall(gullbase, noder);

        foreach (var (tabell, antall) in skrevet)
        {
            _output.WriteLine($"{tabell,-28} {antall,9}");
        }

        resultater.Should().HaveCount(noder.Length,
            "en node som feiler blir logget og hoppet over, og da er settet ikke komplett");

        skrevet["Observation"].Should().Be(resultater.Sum(x => x.Imported));
        skrevet["Location"].Should().Be(resultater.Sum(x => x.Locations));

        skrevet["ObservationTaxonHierarchy"].Should().Be(skrevet["Observation"],
            "én rad per observasjon");
        skrevet["ObservationEntityIndex"].Should().BeGreaterThan(skrevet["Observation"],
            "hver observasjon ligger i flere områder");
    }

    private static int[] LesNoder(string? verdi) =>
        string.IsNullOrWhiteSpace(verdi)
            ? []
            : verdi.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var tall) ? tall : 0)
                .Where(x => x > 0)
                .ToArray();

    /// <summary>
    /// Gjerdet: basen må ha markørtabellen <c>__Gullsett</c>, og den må <b>ikke</b> ha
    /// <c>__ImportWriteTest</c>. Det andre kravet er like viktig som det første — det er
    /// det som holder de to basene fra hverandre.
    /// </summary>
    private static async Task KrevGullbase(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(CASE WHEN name = '__Gullsett' THEN 1 END), "
            + "COUNT(CASE WHEN name = '__ImportWriteTest' THEN 1 END) FROM sys.tables;",
            connection);

        await using var leser = await command.ExecuteReaderAsync();
        await leser.ReadAsync();

        leser.GetInt32(0).Should().Be(1,
            "testen skriver, og skal bare kjøre mot en base som er merket som gullbase");
        leser.GetInt32(1).Should().Be(0,
            "gullbasen skal ikke kunne tømmes av rundturstestene");
    }

    /// <summary>
    /// Tømmer skrivetabellene før en ren kjøring.
    ///
    /// TRUNCATE, IKKE DELETE. Medium har 21 millioner rader i <c>ObservationTaxons</c> og
    /// 5,5 millioner i <c>ObservationEntityIndex</c>, og <c>DELETE</c> sprengte den
    /// 30 sekunder lange standardtimeouten midt i ryddingen — basen ble stående halvtømt.
    /// <c>TRUNCATE</c> er minimalt logget og deallokerer sider i stedet for å slette rad
    /// for rad. Gullbasen har ingen fremmednøkler, så den er tillatt her; kommer det
    /// fremmednøkler senere, feiler den med en tydelig melding framfor å gå i timeout.
    ///
    /// Timeouten er satt høyt likevel: også <c>TRUNCATE</c> tar tid når tabellen er stor.
    /// </summary>
    private static async Task Rydd(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in Skrivetabeller.Reverse())
        {
            await using var command = new SqlCommand($"TRUNCATE TABLE dbo.{tabell};", connection)
            {
                CommandTimeout = 1800
            };

            await command.ExecuteNonQueryAsync();
        }
    }

    private static readonly string[] Skrivetabeller =
    [
        "Location", "Observation", "ObservationDetails", "LocationAreas", "ObservationTags",
        "ObservationTaxons", "ObservationBehaviors", "ObservationProject", "MediaFile",
        "ObservationError", "ObservationLink", "SensitiveObservationData",
        "ObservationTaxonHierarchy", "ObservationEntityIndex"
    ];

    private static ServiceProvider Bygg(string cache, string gullbase, int[] noder)
    {
        var services = new ServiceCollection();

        services.AddLogging(x => x.SetMinimumLevel(LogLevel.Warning));

        services.AddScoped<ImportService>();
        services.AddScoped<ImportJob>();
        services.AddSingleton(new ImportOptions
        {
            Nodes = noder,
            BatchSize = 2500,
            AllowImportWithoutMasking = true
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

        services.AddCacheReader(cache);
        services.AddImportServices();

        services.AddSingleton(new ObservationWriterOptions { ConnectionString = gullbase });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Teller radene i skrivetabellene.
    ///
    /// <c>Observation</c> OG <c>Location</c> TELLES PER NODE. Begge har <c>NodeId</c>, og
    /// uten filteret sammenligner testen hele basens antall mot bare denne kjøringens
    /// sum. Kjører man én node inn i en base som alt har de andre, feiler påstanden med
    /// nøyaktig de andre nodenes rader — en feil i målingen, ikke i importen.
    ///
    /// De øvrige tabellene har ingen <c>NodeId</c> og telles i sin helhet. Påstandene om
    /// dem er forhold som holder uansett hvilke noder basen inneholder: én
    /// <c>ObservationTaxonHierarchy</c>-rad per observasjon, og flere
    /// <c>ObservationEntityIndex</c>-rader enn observasjoner.
    /// </summary>
    private static async Task<Dictionary<string, int>> LesAntall(string connectionString, int[] noder)
    {
        string[] perNode = ["Observation", "Location"];

        string[] tabeller =
        [
            "Observation", "ObservationDetails", "Location", "LocationAreas", "ObservationTags",
            "ObservationTaxons", "ObservationBehaviors", "ObservationProject", "MediaFile",
            "ObservationError", "ObservationLink", "SensitiveObservationData",
            "ObservationTaxonHierarchy", "ObservationEntityIndex"
        ];

        var nodeliste = string.Join(", ", noder);
        var resultat = new Dictionary<string, int>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in tabeller)
        {
            var filter = perNode.Contains(tabell) ? $" WHERE NodeId IN ({nodeliste})" : string.Empty;

            await using var command = new SqlCommand(
                $"SELECT COUNT(*) FROM dbo.{tabell}{filter};", connection) { CommandTimeout = 600 };

            resultat[tabell] = (int)(await command.ExecuteScalarAsync())!;
        }

        return resultat;
    }
}
