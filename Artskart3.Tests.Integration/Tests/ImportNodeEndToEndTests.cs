extern alias workers;

using System.Diagnostics;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
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
/// Kjører en ekte node fra cachen gjennom hele kjeden og inn i testbasen. Dette er den
/// første målingen av om importen virker på ekte data — alt annet er enhetstester og
/// smoketester mot deler av den.
///
/// KREVER TO TILKOBLINGER OG EN NODE:
///
///   $env:ARTSKART_CACHE_CONNECTION = "data source=localhost;initial catalog=Artskart2Cache-...;Integrated Security=true;TrustServerCertificate=True"
///   $env:ARTSKART_WRITE_TEST_CONNECTION = "data source=localhost;initial catalog=Artskart3ImportWriteTest;Integrated Security=true;TrustServerCertificate=True"
///   $env:ARTSKART_IMPORT_NODE = "43"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~ImportNodeEndToEnd"
///
/// REFERANSEDATAENE LESES FRA TESTBASEN, ikke fra indeksbasen: taksonomi, områder,
/// organisasjoner, funntyper og kategorier er kopiert dit, så kjøringen rører ingen
/// annen base enn den ene den skriver til.
///
/// DATAKILDEREGISTERET ER IKKE I BRUK. <c>DataSource</c>-tabellen er tom overalt — den
/// fylles av speilingen fra Artskart 2, som er åpent spørsmål 2e — så nodens
/// innstillinger settes her i stedet for å slås opp.
///
/// ALIASTABELLEN ER TOM. Poster fra en institusjonskode som bare finnes som alias blir
/// derfor avvist med <c>InstitutionCodeNotKnown</c>, og det er en måling verdt å ha:
/// den sier hvor mye aliasene betyr for nettopp denne noden.
/// </summary>
public class ImportNodeEndToEndTests
{

    private readonly ITestOutputHelper _output;

    public ImportNodeEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Teller utfallene per valideringskode. Det er rapporten vi er ute etter.</summary>
    private sealed class Tellende : IImportOutcomeSink
    {
        public Dictionary<RecordValidationCode, int> Avvisninger { get; } = [];

        public Dictionary<RecordValidationCode, string> Eksempler { get; } = [];

        public int Ødelagte { get; private set; }

        public int Slettede { get; private set; }

        public void RecordRejected(CacheReadItem item, ImportRecordOutcome outcome)
        {
            var kode = outcome.Errors.Count > 0 ? outcome.Errors[0].ValidationCode : RecordValidationCode.Error;

            Avvisninger[kode] = Avvisninger.GetValueOrDefault(kode) + 1;

            if (!Eksempler.ContainsKey(kode) && outcome.Errors.Count > 0)
            {
                Eksempler[kode] = $"{item.ProxyId}: {outcome.Errors[0].ErrorContext}";
            }
        }

        public void RecordMalformed(CacheReadItem item) => Ødelagte++;

        public void RecordDeleted(CacheReadItem item, string proxyId) => Slettede++;

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Importerer_en_ekte_node()
    {
        var cache = Environment.GetEnvironmentVariable("ARTSKART_CACHE_CONNECTION");
        var skrivebase = Environment.GetEnvironmentVariable("ARTSKART_WRITE_TEST_CONNECTION");
        var node = Environment.GetEnvironmentVariable("ARTSKART_IMPORT_NODE");

        if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(skrivebase)
            || !int.TryParse(node, out var nodeId))
        {
            _output.WriteLine("ARTSKART_CACHE_CONNECTION, ARTSKART_WRITE_TEST_CONNECTION og "
                              + "ARTSKART_IMPORT_NODE må være satt - testen gjør ingenting.");
            return;
        }

        await ObservationWriterRoundTripTests.KrevTestbase(skrivebase);
        await ObservationWriterRoundTripTests.Rydd(skrivebase);

        await using var leverandør = Bygg(cache, skrivebase);
        using var scope = leverandør.CreateScope();
        var sp = scope.ServiceProvider;

        var kjedefabrikk = sp.GetRequiredService<ImportStepChainFactory>();
        var utfall = new Tellende();

        var prosessor = new ImportBatchProcessor(
            sp.GetRequiredService<ICacheRecordReader>(),
            sp.GetRequiredService<IObservationWriter>(),
            () => kjedefabrikk.Create(),
            sp.GetRequiredService<ILogger<ImportBatchProcessor>>(),
            utfall);

        var klokke = Stopwatch.StartNew();

        var resultat = await prosessor.RunAsync(
            new ImportNodeRequest
            {
                Settings = new ImportRunSettings
                {
                    NodeId = nodeId,
                    ProcessEngineId = 1,
                    ProcessedAt = DateTime.UtcNow,
                    ImportedAt = DateTime.UtcNow
                },

                // Små bunter her, slik at flere runder faktisk publiseres.
                BatchSize = 2500
            },
            CancellationToken.None);

        klokke.Stop();

        var skrevet = await LesAntall(skrivebase);

        _output.WriteLine($"=== Node {nodeId} ===");
        _output.WriteLine($"Lest:              {resultat.Read}");
        _output.WriteLine($"Importert:         {resultat.Imported}");
        _output.WriteLine($"Avvist:            {resultat.Rejected}");
        _output.WriteLine($"Slettet:           {resultat.Deleted} (+{resultat.DeletedWithoutIdentity} uten identitet)");
        _output.WriteLine($"Ødelagte blober:   {resultat.Malformed}");
        _output.WriteLine($"Runder:            {resultat.Rounds}");
        _output.WriteLine($"Lokaliteter:       {resultat.Locations}");
        _output.WriteLine($"Tid:               {klokke.Elapsed.TotalSeconds:F1} s "
                          + $"({resultat.Read / Math.Max(klokke.Elapsed.TotalSeconds, 0.001):F0} poster/s)");

        _output.WriteLine(string.Empty);
        _output.WriteLine("Avvisninger:");

        foreach (var (kode, antall) in utfall.Avvisninger.OrderByDescending(x => x.Value))
        {
            var andel = 100.0 * antall / Math.Max(resultat.Read, 1);

            _output.WriteLine($"  {kode,-28} {antall,7}  ({andel:F1} %)");

            if (utfall.Eksempler.TryGetValue(kode, out var eksempel))
            {
                _output.WriteLine($"      {Kort(eksempel)}");
            }
        }

        _output.WriteLine(string.Empty);
        _output.WriteLine("Rader i basen:");

        foreach (var (tabell, antall) in skrevet.OrderByDescending(x => x.Value))
        {
            _output.WriteLine($"  {tabell,-28} {antall,7}");
        }

        resultat.Read.Should().BeGreaterThan(0, "noden skal finnes i cachen");
        (resultat.Imported + resultat.Rejected + resultat.Deleted
         + resultat.DeletedWithoutIdentity + resultat.Malformed)
            .Should().Be(resultat.Read, "hver post skal ende i nøyaktig én kategori");

        skrevet["Observation"].Should().Be(resultat.Imported,
            "hver importert post skal være én rad");
        skrevet["ObservationDetails"].Should().Be(resultat.Imported,
            "detaljraden deler Id med observasjonen");
        skrevet["Location"].Should().Be(resultat.Locations);
    }

    /// <summary>
    /// Samme node, men gjennom <c>ImportJob</c> — altså med oppslaget i
    /// <c>DataSource</c>, prosessmotor-Id-en og maskeringssperren.
    ///
    /// FORSKJELLEN FRA TESTEN OVER er hvem som bestemmer. Der settes nodens
    /// innstillinger i testen; her kommer de fra registeret, slik de gjør i drift.
    ///
    /// KREVER EN RAD I <c>DataSource</c> for noden, og en rad i
    /// <c>ProsessEngineHistory</c>. Begge må legges inn for hånd i testbasen: registeret
    /// fylles ellers av speilingen fra Artskart 2 (plan 4.9.2, åpent spørsmål 2e).
    /// </summary>
    [Fact]
    public async Task Kjorer_jobben_mot_en_ekte_node()
    {
        var cache = Environment.GetEnvironmentVariable("ARTSKART_CACHE_CONNECTION");
        var skrivebase = Environment.GetEnvironmentVariable("ARTSKART_WRITE_TEST_CONNECTION");
        var node = Environment.GetEnvironmentVariable("ARTSKART_IMPORT_NODE");

        if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(skrivebase)
            || !int.TryParse(node, out var nodeId))
        {
            _output.WriteLine("ARTSKART_CACHE_CONNECTION, ARTSKART_WRITE_TEST_CONNECTION og "
                              + "ARTSKART_IMPORT_NODE må være satt - testen gjør ingenting.");
            return;
        }

        await ObservationWriterRoundTripTests.KrevTestbase(skrivebase);
        await ObservationWriterRoundTripTests.Rydd(skrivebase);

        await using var leverandør = Bygg(cache, skrivebase, nodeId);
        using var scope = leverandør.CreateScope();

        var jobb = scope.ServiceProvider.GetRequiredService<ImportJob>();

        var resultat = await jobb.ImportNodeAsync(nodeId, changedAfter: null, CancellationToken.None);

        _output.WriteLine($"=== ImportJob, node {nodeId} ===");
        _output.WriteLine($"Lest {resultat.Read}, importert {resultat.Imported}, avvist {resultat.Rejected}, "
                          + $"{resultat.Rounds} runder, {resultat.Locations} lokaliteter, "
                          + $"{resultat.Elapsed.TotalSeconds:F1} s");

        var skrevet = await LesAntall(skrivebase);

        resultat.Read.Should().BeGreaterThan(0);
        skrevet["Observation"].Should().Be(resultat.Imported);
        skrevet["Location"].Should().Be(resultat.Locations);

        // De avledede tabellene (fase 7) skrives i samme transaksjon som
        // observasjonene. Mangler de, er postene usynlige for alle områdefiltre.
        skrevet["ObservationTaxonHierarchy"].Should().Be(resultat.Imported,
            "én rad per observasjon");
        skrevet["ObservationEntityIndex"].Should().BeGreaterThan(resultat.Imported,
            "hver observasjon ligger i flere områder");

        _output.WriteLine($"ObservationTaxonHierarchy {skrevet["ObservationTaxonHierarchy"]}, "
                          + $"ObservationEntityIndex {skrevet["ObservationEntityIndex"]}");
    }

    /// <summary>
    /// Sperren skal stå i veien når den ikke er slått av med vilje — også når jobben
    /// kalles herfra.
    /// </summary>
    [Fact]
    public async Task Jobben_nekter_a_kjore_uten_at_maskeringssperren_er_slatt_av()
    {
        var cache = Environment.GetEnvironmentVariable("ARTSKART_CACHE_CONNECTION");
        var skrivebase = Environment.GetEnvironmentVariable("ARTSKART_WRITE_TEST_CONNECTION");
        var node = Environment.GetEnvironmentVariable("ARTSKART_IMPORT_NODE");

        if (string.IsNullOrWhiteSpace(cache) || string.IsNullOrWhiteSpace(skrivebase)
            || !int.TryParse(node, out var nodeId))
        {
            _output.WriteLine("Miljøvariablene er ikke satt - testen gjør ingenting.");
            return;
        }

        await using var leverandør = Bygg(cache, skrivebase, nodeId, tillatUtenMaskering: false);
        using var scope = leverandør.CreateScope();

        var jobb = scope.ServiceProvider.GetRequiredService<ImportJob>();

        var handling = async () =>
            await jobb.ImportNodeAsync(nodeId, changedAfter: null, CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AllowImportWithoutMasking*");
    }

    private static string Kort(string tekst) => tekst.Length <= 110 ? tekst : tekst[..110] + "...";

    private static ServiceProvider Bygg(
        string cache,
        string skrivebase,
        int? nodeId = null,
        bool tillatUtenMaskering = true)
    {
        var services = new ServiceCollection();

        services.AddLogging(x => x.SetMinimumLevel(LogLevel.Warning));

        // Jobben og tjenesten bak den. Innstillingene settes her i stedet for fra
        // konfigurasjonsfilen, slik at testen bestemmer hvilken node som kjøres og om
        // maskeringssperren er slått av.
        services.AddScoped<ImportService>();
        services.AddScoped<ImportJob>();
        services.AddSingleton(new ImportOptions
        {
            Nodes = nodeId is null ? [] : [nodeId.Value],
            BatchSize = 2500,
            AllowImportWithoutMasking = tillatUtenMaskering
        });

        services.AddDbContext<ArtskartDbContext>(options =>
        {
            options.UseSqlServer(skrivebase, sql =>
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

        services.AddSingleton(new ObservationWriterOptions { ConnectionString = skrivebase });

        return services.BuildServiceProvider();
    }

    private static async Task<Dictionary<string, int>> LesAntall(string connectionString)
    {
        string[] tabeller =
        [
            "Observation", "ObservationDetails", "Location", "LocationAreas", "ObservationTags",
            "ObservationTaxons", "ObservationBehaviors", "ObservationProject", "MediaFile",
            "ObservationError", "ObservationLink", "SensitiveObservationData",
            "ObservationTaxonHierarchy", "ObservationEntityIndex"
        ];

        var resultat = new Dictionary<string, int>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in tabeller)
        {
            await using var command = new SqlCommand($"SELECT COUNT(*) FROM dbo.{tabell};", connection);

            resultat[tabell] = (int)(await command.ExecuteScalarAsync())!;
        }

        return resultat;
    }
}
