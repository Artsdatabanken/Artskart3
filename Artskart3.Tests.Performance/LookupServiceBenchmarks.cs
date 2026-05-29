using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Ytelsestester for LookupService-laget mot en ekte database.
///
/// Kjøring:
///   dotnet run -c Release
///
/// Tilkoblingsstrengen leses fra miljøvariabelen ARTSKART_BENCH_CONNECTION_STRING,
/// eller fra bruker-secrets hvis miljøvariabelen ikke er satt.
///
/// Resultater eksporteres til BenchmarkDotNet.Artifacts/ som markdown og HTML.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
public class LookupServiceBenchmarks
{
    private LookupService _lookupService = null!;
    private ArtskartDbContext _dbContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<LookupServiceBenchmarks>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["ARTSKART_BENCH_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "Tilkoblingsstreng ikke funnet. Sett bruker-secret eller miljøvariabel: ARTSKART_BENCH_CONNECTION_STRING");

        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(connectionString, x => x.UseNetTopologySuite())
            .Options;

        _dbContext = new ArtskartDbContext(options);

        var repository = new LookupRepository(_dbContext);
        _lookupService = new LookupService(repository);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _dbContext.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // GetCategories — tester henting av kategorityper med kategorier
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetCategories()
    {
        _ = await _lookupService.GetCategoriesAsync();
    }

    // -----------------------------------------------------------------------
    // GetAreas — tester henting av områdetyper med områder
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetAreas()
    {
        _ = await _lookupService.GetAreasAsync();
    }

    // -----------------------------------------------------------------------
    // GetInstitutions — tester henting av institusjoner
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetInstitutions()
    {
        _ = await _lookupService.GetInstitutionsAsync();
    }

    // -----------------------------------------------------------------------
    // GetTaxonGroups — tester henting av artsgrupper
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetTaxonGroups()
    {
        _ = await _lookupService.GetTaxonGroupsAsync();
    }

    // -----------------------------------------------------------------------
    // GetBehaviors — tester henting av atferd
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetBehaviors()
    {
        _ = await _lookupService.GetBehaviorsAsync();
    }

    // -----------------------------------------------------------------------
    // GetBasisOfRecords — tester henting av funntyper
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetBasisOfRecords()
    {
        _ = await _lookupService.GetBasisOfRecordsAsync();
    }
}
