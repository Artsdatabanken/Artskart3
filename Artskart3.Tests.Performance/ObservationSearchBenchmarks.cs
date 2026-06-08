using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Ytelsestester for observasjonssøk med område- og institusjonsfiltre via ObservationAreaIndex.
/// Tester ulike kombinasjoner av kommune, fylke, havområde, verneområde og institusjon.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
public class ObservationSearchBenchmarks
{
    private SearchRepository _repository = null!;
    private ArtskartDbContext _dbContext = null!;

    // Kommuner (AreaTypeId = 1) — sortert etter antall observasjoner
    private static readonly string[] Municipalities1  = ["5001"];
    private static readonly string[] Municipalities3  = ["5001", "301", "4601"];
    private static readonly string[] Municipalities15 = ["5001", "301", "4601", "3203", "5021", "4204", "3201", "3301", "1103", "5501", "4206", "3411", "3909", "4203", "3312"];

    // Fylker (AreaTypeId = 2)
    private static readonly string[] Counties1 = ["50"];
    private static readonly string[] Counties3 = ["50", "46", "34"];

    // Havområder (AreaTypeId = 4)
    private static readonly string[] OceanAreas1 = ["91"];
    private static readonly string[] OceanAreas3 = ["91", "94", "92"];

    // Verneområder (AreaTypeId = 3)
    private static readonly string[] RestrictedAreas1 = ["Naturbase VV00001897"];
    private static readonly string[] RestrictedAreas3 = ["Naturbase VV00001897", "Naturbase VV00001878", "Naturbase VV00003273"];

    // Institusjoner (AreaTypeId = 5)
    private static readonly int[] Institutions1 = [1];
    private static readonly int[] Institutions3 = [1, 3127, 3100];

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
        _repository = new SearchRepository(_dbContext, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _dbContext.DisposeAsync();
    }

    private static ObservationSearchFilterDto Paginated(
        string[]? municipalityIds = null,
        string[]? countyIds = null,
        string[]? oceanAreaIds = null,
        string[]? restrictedAreaIds = null,
        int[]? organizationIds = null) => new()
    {
        PageNumber = 1,
        ResultsPerPage = 10,
        MunicipalityIds = municipalityIds,
        CountyIds = countyIds,
        OceanAreaIds = oceanAreaIds,
        RestrictedAreaIds = restrictedAreaIds,
        OrganizationIds = organizationIds
    };

    // -----------------------------------------------------------------------
    // Kommunefilter
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task Municipality_1()
    {
        _ = await _repository.GetObservationsAsync(Paginated(municipalityIds: Municipalities1));
    }

    [Benchmark]
    public async Task Municipality_3()
    {
        _ = await _repository.GetObservationsAsync(Paginated(municipalityIds: Municipalities3));
    }

    [Benchmark]
    public async Task Municipality_15()
    {
        _ = await _repository.GetObservationsAsync(Paginated(municipalityIds: Municipalities15));
    }

    // -----------------------------------------------------------------------
    // Fylkesfilter
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task County_1()
    {
        _ = await _repository.GetObservationsAsync(Paginated(countyIds: Counties1));
    }

    [Benchmark]
    public async Task County_3()
    {
        _ = await _repository.GetObservationsAsync(Paginated(countyIds: Counties3));
    }

    // -----------------------------------------------------------------------
    // Havområder
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task OceanArea_1()
    {
        _ = await _repository.GetObservationsAsync(Paginated(oceanAreaIds: OceanAreas1));
    }

    [Benchmark]
    public async Task OceanArea_3()
    {
        _ = await _repository.GetObservationsAsync(Paginated(oceanAreaIds: OceanAreas3));
    }

    // -----------------------------------------------------------------------
    // Verneområder
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task RestrictedArea_1()
    {
        _ = await _repository.GetObservationsAsync(Paginated(restrictedAreaIds: RestrictedAreas1));
    }

    [Benchmark]
    public async Task RestrictedArea_3()
    {
        _ = await _repository.GetObservationsAsync(Paginated(restrictedAreaIds: RestrictedAreas3));
    }

    // -----------------------------------------------------------------------
    // Institusjoner
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task Institution_1()
    {
        _ = await _repository.GetObservationsAsync(Paginated(organizationIds: Institutions1));
    }

    [Benchmark]
    public async Task Institution_3()
    {
        _ = await _repository.GetObservationsAsync(Paginated(organizationIds: Institutions3));
    }

    // -----------------------------------------------------------------------
    // Kombinert: ett områdefilter + én institusjon (OR-semantikk via Union)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task Municipality1_Plus_Institution1()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(municipalityIds: Municipalities1, organizationIds: Institutions1));
    }

    [Benchmark]
    public async Task County1_Plus_Institution1()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(countyIds: Counties1, organizationIds: Institutions1));
    }

    [Benchmark]
    public async Task OceanArea1_Plus_Institution1()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(oceanAreaIds: OceanAreas1, organizationIds: Institutions1));
    }

    [Benchmark]
    public async Task RestrictedArea1_Plus_Institution1()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(restrictedAreaIds: RestrictedAreas1, organizationIds: Institutions1));
    }
}
