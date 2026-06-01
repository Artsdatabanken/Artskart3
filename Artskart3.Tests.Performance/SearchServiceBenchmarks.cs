using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Ytelsestester for SearchService-laget mot en ekte database.
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
public class SearchServiceBenchmarks
{
    private SearchService _searchService = null!;
    private ArtskartDbContext _dbContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<SearchServiceBenchmarks>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["ARTSKART_BENCH_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "Tilkoblingsstreng ikke funnet. Sett bruker-secret eller miljøvariabel: ARTSKART_BENCH_CONNECTION_STRING");

        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(connectionString, x => x.UseNetTopologySuite())
            .Options;

        _dbContext = new ArtskartDbContext(options);

        var repository = new SearchRepository(_dbContext, NullLogger<SearchRepository>.Instance);
        _searchService = new SearchService(repository);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _dbContext.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // SearchTaxons — tester tre-nivå søkestrategi (eksakt / starter-med / inneholder)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task SearchTaxons_ExactMatch()
    {
        // Vanlig artsnavn som skal treffe eksakt-match-grenen
        _ = await _searchService.GetTaxonsAsync("parus major", 20);
    }

    [Benchmark]
    public async Task SearchTaxons_StartsWithMatch()
    {
        _ = await _searchService.GetTaxonsAsync("par", 20);
    }

    [Benchmark]
    public async Task SearchTaxons_ContainsMatch()
    {
        _ = await _searchService.GetTaxonsAsync("kjøtt", 20);
    }

    [Benchmark]
    public async Task SearchTaxons_MaxResults_1000()
    {
        _ = await _searchService.GetTaxonsAsync("a", 1000);
    }

    // -----------------------------------------------------------------------
    // GetLocations — tester aggregeringsspørring og GeoJSON-serialisering
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetLocations_NoFilter_Default1000()
    {
        _ = await _searchService.GetLocationsAsync(new LocationSearchFilterDto());
    }

    [Benchmark]
    public async Task GetLocations_WithTaxonGroupFilter()
    {
        _ = await _searchService.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = "1",
            MaxResults = 500
        });
    }

    [Benchmark]
    public async Task GetLocations_WithPrecisionFilter()
    {
        _ = await _searchService.GetLocationsAsync(new LocationSearchFilterDto
        {
            CoordinatePrecisionFrom = 1,
            CoordinatePrecisionTo = 100,
            MaxResults = 1000
        });
    }

    // -----------------------------------------------------------------------
    // GetAreas — tester henting av områder og beregning av tyngdepunkt
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetAreas_AllCountiesAndMunicipalities()
    {
        _ = await _searchService.GetAreasByTypeIdsAsync([1, 2]);
    }

    [Benchmark]
    public async Task GetLocations_WithTaxonGroupAndPrecisionFilter()
    {
        _ = await _searchService.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = "1",
            CoordinatePrecisionFrom = 1,
            CoordinatePrecisionTo = 1000,
            MaxResults = 500
        });
    }

    [Benchmark]
    public async Task GetAreas_MunicipalitiesOnly()
    {
        _ = await _searchService.GetAreasByTypeIdsAsync([1]);
    }

    [Benchmark]
    public async Task GetAreas_CountiesOnly()
    {
        _ = await _searchService.GetAreasByTypeIdsAsync([2]);
    }

    [Benchmark]
    public async Task GetLocations_MaxResults_5000()
    {
        _ = await _searchService.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = 5000 });
    }
}
