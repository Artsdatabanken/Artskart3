using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Benchmarks for the SearchService layer against a real database.
///
/// Usage:
///   dotnet run -c Release
///
/// The connection string is read from the ARTSKART_BENCH_CONNECTION_STRING
/// environment variable, or falls back to appsettings.Performance.json.
///
/// Results are exported to BenchmarkDotNet.Artifacts/ as markdown and HTML.
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
        var connectionString = Environment.GetEnvironmentVariable("ARTSKART_BENCH_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Set environment variable ARTSKART_BENCH_CONNECTION_STRING to a production-like database connection string.");

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
    // SearchTaxons — covers the three-level match strategy (exact / starts-with / contains)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task SearchTaxons_ExactMatch()
    {
        // Use a common species name that should hit the exact-match branch
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
    // GetLocations — covers aggregation query + GeoJSON serialization
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
    // GetAreas — covers area retrieval and centroid calculation
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task GetAreas_AllCountiesAndMunicipalities()
    {
        _ = await _searchService.GetAreasByTypeIdsAsync(1, 2);
    }
}
