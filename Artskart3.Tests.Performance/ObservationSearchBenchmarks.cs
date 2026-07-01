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
/// Ytelsestester for observasjonssøk med område- og institusjonsfiltre via ObservationEntityIndex.
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
    private static readonly string[] Municipalities1 = ["5001"];
    private static readonly string[] Municipalities3 = ["5001", "301", "4601"];
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

    // Institusjoner (via ObservationEntityIndex med EntityTypeId = Organization)
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
        _repository = new SearchRepository(_dbContext, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());
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
    // Kombinert: ett områdefilter + én institusjon (OR-semantikk)
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

    // -----------------------------------------------------------------------
    // Flere områdetyper samtidig (tester OR-kombineringen i EXISTS)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task Municipality1_Plus_County1()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(municipalityIds: Municipalities1, countyIds: Counties1));
    }

    [Benchmark]
    public async Task Municipality3_Plus_County3_Plus_Institution3()
    {
        _ = await _repository.GetObservationsAsync(
            Paginated(municipalityIds: Municipalities3, countyIds: Counties3, organizationIds: Institutions3));
    }

    [Benchmark]
    public async Task AllAreaTypes_Combined()
    {
        _ = await _repository.GetObservationsAsync(Paginated(
            municipalityIds: Municipalities3,
            countyIds: Counties3,
            oceanAreaIds: OceanAreas3,
            restrictedAreaIds: RestrictedAreas3,
            organizationIds: Institutions3));
    }

    // -----------------------------------------------------------------------
    // Tekstsøk (LIKE-mønstre mot Taxon/TaxonName-tabeller)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task TextSearch_PopularName()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            PreferredPopularName = "blåmeis"
        });
    }

    [Benchmark]
    public async Task TextSearch_ScientificName()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            ScientificName = "Parus major"
        });
    }

    [Benchmark]
    public async Task TextSearch_Author()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            Author = "Linnaeus"
        });
    }

    [Benchmark]
    public async Task TextSearch_PopularName_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            PreferredPopularName = "blåmeis",
            MunicipalityIds = Municipalities1
        });
    }

    // -----------------------------------------------------------------------
    // Paginering i dybden (Skip-ytelse ved store offsets)
    // -----------------------------------------------------------------------

    [Benchmark]
    public async Task Pagination_Page1_PerPage50()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 50,
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task Pagination_Page10_PerPage50()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 10,
            ResultsPerPage = 50,
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task Pagination_Page100_PerPage10()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 100,
            ResultsPerPage = 10,
            MunicipalityIds = Municipalities1
        });
    }

    // -----------------------------------------------------------------------
    // Kombinerte filtre (kategori, atferd, innsamlingsmetode + område)
    // -----------------------------------------------------------------------

    private static readonly int[] Categories1 = [9];
    private static readonly int[] Categories3 = [9, 11, 12];
    private static readonly int[] Behaviors1 = [6];
    private static readonly int[] Behaviors3 = [6, 2, 7];
    private static readonly int[] BasisOfRecords1 = [14];
    private static readonly int[] BasisOfRecords3 = [14, 1, 13];

    [Benchmark]
    public async Task Category1_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            CategoryIds = Categories1,
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task Category3_Plus_Municipality3()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            CategoryIds = Categories3,
            MunicipalityIds = Municipalities3
        });
    }

    [Benchmark]
    public async Task Behavior1_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            BehaviorIds = Behaviors1,
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task BasisOfRecord1_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            BasisOfRecordIds = BasisOfRecords1,
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task CoordinatePrecision_Range_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            CoordinatePrecision = new CoordinatePrecisionDto { From = 1, To = 100 },
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task Period_2020to2024_Plus_Municipality1()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 10,
            Period = new PeriodDto { From = 2020, To = 2024 },
            MunicipalityIds = Municipalities1
        });
    }

    [Benchmark]
    public async Task RealisticCombo_Text_Category_Area_Period()
    {
        _ = await _repository.GetObservationsAsync(new ObservationSearchFilterDto
        {
            PageNumber = 1,
            ResultsPerPage = 50,
            PreferredPopularName = "meis",
            CategoryIds = Categories1,
            MunicipalityIds = Municipalities3,
            Period = new PeriodDto { From = 2015, To = 2025 }
        });
    }
}
