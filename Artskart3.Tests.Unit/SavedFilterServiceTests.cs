using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Tests.Unit;

public class SavedFilterServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private readonly ArtskartDbContext _context;
    private readonly ISavedFilterService _sut;

    public SavedFilterServiceTests()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ArtskartDbContext(options);
        _sut = new SavedFilterService(_context);
    }

    [Fact]
    public async Task CreateAsync_StripsPaginationAndStoresExtent()
    {
        var request = Request("Fugler", extent: new MapExtentDto { MinX = 1, MinY = 2, MaxX = 3, MaxY = 4 });
        request.Filter.PageNumber = 3;
        request.Filter.ResultsPerPage = 50;

        var created = await _sut.CreateAsync(UserId, request);

        created.Filter.PageNumber.Should().BeNull();
        created.Filter.ResultsPerPage.Should().BeNull();
        created.Filter.TaxonGroupIds.Should().Equal(7);
        created.Extent.Should().BeEquivalentTo(new MapExtentDto { MinX = 1, MinY = 2, MaxX = 3, MaxY = 4 });
    }

    [Fact]
    public async Task CreateAsync_WithoutExtent_KeepsExtentNull()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler"));

        created.Extent.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndExposesPublicIdAsId()
    {
        var created = await _sut.CreateAsync(UserId, Request("  Fugler  "));

        var entity = await _context.SavedFilters.SingleAsync();
        created.Name.Should().Be("Fugler");
        created.Id.Should().Be(entity.PublicId).And.NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedAtAsUtc()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler"));

        created.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task CreateAsync_AsDefault_ClearsOnlyOwnPreviousDefault()
    {
        var first = await _sut.CreateAsync(UserId, Request("Først", isDefault: true));
        var otherUsers = await _sut.CreateAsync(OtherUserId, Request("Andres", isDefault: true));

        var second = await _sut.CreateAsync(UserId, Request("Andre", isDefault: true));

        var own = await _sut.GetUserFiltersAsync(UserId);
        own.Single(f => f.Id == first.Id).IsDefault.Should().BeFalse();
        own.Single(f => f.Id == second.Id).IsDefault.Should().BeTrue();
        (await _sut.GetUserFiltersAsync(OtherUserId)).Single(f => f.Id == otherUsers.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserFiltersAsync_ReturnsOnlyOwnFiltersNewestFirst()
    {
        var older = Entity(UserId, "Eldst");
        older.CreatedAt = DateTime.UtcNow.AddDays(-2);
        var newer = Entity(UserId, "Nyest");
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);
        _context.SavedFilters.AddRange(older, newer, Entity(OtherUserId, "Andres"));
        await _context.SaveChangesAsync();

        var result = await _sut.GetUserFiltersAsync(UserId);

        result.Select(f => f.Name).Should().Equal("Nyest", "Eldst");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndClearsDefault()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler", isDefault: true));

        var deleted = await _sut.DeleteAsync(created.Id, UserId);

        deleted.Should().BeTrue();
        var entity = await _context.SavedFilters.IgnoreQueryFilters().SingleAsync();
        entity.IsDeleted.Should().BeTrue();
        entity.DeletedAt.Should().NotBeNull();
        entity.IsDefault.Should().BeFalse();
        (await _sut.GetUserFiltersAsync(UserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_ReturnsFalse()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler"));
        await _sut.DeleteAsync(created.Id, UserId);

        var deletedAgain = await _sut.DeleteAsync(created.Id, UserId);

        deletedAgain.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersFilter_ReturnsFalseAndKeepsIt()
    {
        var created = await _sut.CreateAsync(OtherUserId, Request("Andres"));

        var deleted = await _sut.DeleteAsync(created.Id, UserId);

        deleted.Should().BeFalse();
        (await _sut.GetUserFiltersAsync(OtherUserId)).Should().ContainSingle();
    }

    [Fact]
    public async Task SetDefaultAsync_SwitchesDefault()
    {
        var first = await _sut.CreateAsync(UserId, Request("Først", isDefault: true));
        var second = await _sut.CreateAsync(UserId, Request("Andre"));

        var updated = await _sut.SetDefaultAsync(second.Id, UserId, true);

        updated.Should().BeTrue();
        var own = await _sut.GetUserFiltersAsync(UserId);
        own.Single(f => f.Id == first.Id).IsDefault.Should().BeFalse();
        own.Single(f => f.Id == second.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_False_ClearsDefault()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler", isDefault: true));

        await _sut.SetDefaultAsync(created.Id, UserId, false);

        (await _sut.GetUserFiltersAsync(UserId)).Single().IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_OtherUsersFilter_ReturnsFalseAndChangesNothing()
    {
        var own = await _sut.CreateAsync(UserId, Request("Mitt", isDefault: true));
        var others = await _sut.CreateAsync(OtherUserId, Request("Andres"));

        var updated = await _sut.SetDefaultAsync(others.Id, UserId, true);

        updated.Should().BeFalse();
        (await _sut.GetUserFiltersAsync(UserId)).Single(f => f.Id == own.Id).IsDefault.Should().BeTrue();
        (await _sut.GetUserFiltersAsync(OtherUserId)).Single().IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_DeletedFilter_ReturnsFalse()
    {
        var created = await _sut.CreateAsync(UserId, Request("Fugler"));
        await _sut.DeleteAsync(created.Id, UserId);

        var updated = await _sut.SetDefaultAsync(created.Id, UserId, true);

        updated.Should().BeFalse();
    }

    /// <summary>
    /// Feiler hvis ObservationSearchFilterDto får et felt som ikke er satt i FullFilter,
    /// så nye felt blir testet for at de overlever lagring og henting.
    /// </summary>
    [Fact]
    public async Task CreateAsync_RoundTripsEveryFilterField()
    {
        var filter = FullFilter();
        UnsetProperties(filter).Should().BeEquivalentTo(
            [nameof(filter.PageNumber), nameof(filter.ResultsPerPage)],
            "alle felt i ObservationSearchFilterDto må settes i FullFilter");

        await _sut.CreateAsync(UserId, new CreateSavedFilterRequestDto { Name = "Alt", Filter = filter });

        (await _sut.GetUserFiltersAsync(UserId)).Single().Filter.Should().BeEquivalentTo(FullFilter());
    }

    /// <summary>
    /// Lagrede filtre leses lenge etter at de ble skrevet. Feiler hvis et felt i
    /// ObservationSearchFilterDto får nytt navn eller fjernes, siden JSON-en under
    /// da ikke lenger leses inn. Nye felt legges til både her og i FullFilter.
    /// </summary>
    [Fact]
    public async Task GetUserFiltersAsync_ReadsPreviouslySavedFilterJson()
    {
        var entity = Entity(UserId, "Lagret tidligere");
        entity.FilterJson = """
            {
              "TaxonGroupIds": [1, 2],
              "TaxonIds": [31133],
              "CategoryIds": [3],
              "OrganizationIds": [4],
              "MunicipalityIds": ["5001"],
              "CountyIds": ["50"],
              "RestrictedAreaIds": ["r1"],
              "OceanAreaIds": ["o1"],
              "BehaviorIds": [6],
              "BasisOfRecordIds": [7],
              "RegistrationStatusId": 1,
              "CoordinatePrecision": { "From": 10, "To": 100 },
              "Period": { "From": 2000, "To": 2020, "Months": [5, 6] },
              "DatasetOrgId": 8,
              "ProjectOrgId": 9,
              "ObservationIds": [10],
              "WithImages": true,
              "PageNumber": null,
              "ResultsPerPage": null,
              "IsPaginated": false
            }
            """;
        _context.SavedFilters.Add(entity);
        await _context.SaveChangesAsync();

        (await _sut.GetUserFiltersAsync(UserId)).Single().Filter.Should().BeEquivalentTo(FullFilter());
    }

    private static CreateSavedFilterRequestDto Request(string name, bool isDefault = false, MapExtentDto? extent = null) => new()
    {
        Name = name,
        Filter = new ObservationSearchFilterDto { TaxonGroupIds = [7] },
        Extent = extent,
        IsDefault = isDefault,
    };

    private static SavedFilter Entity(Guid userId, string name) => new()
    {
        UserId = userId,
        Name = name,
        FilterJson = "{}",
    };

    private static ObservationSearchFilterDto FullFilter() => new()
    {
        TaxonGroupIds = [1, 2],
        TaxonIds = [31133],
        CategoryIds = [3],
        OrganizationIds = [4],
        MunicipalityIds = ["5001"],
        CountyIds = ["50"],
        RestrictedAreaIds = ["r1"],
        OceanAreaIds = ["o1"],
        BehaviorIds = [6],
        BasisOfRecordIds = [7],
        RegistrationStatusId = 1,
        CoordinatePrecision = new CoordinatePrecisionDto { From = 10, To = 100 },
        Period = new PeriodDto { From = 2000, To = 2020, Months = [5, 6] },
        DatasetOrgId = 8,
        ProjectOrgId = 9,
        ObservationIds = [10],
        WithImages = true,
    };

    private static IEnumerable<string> UnsetProperties(object dto, string prefix = "")
    {
        foreach (var property in dto.GetType().GetProperties().Where(p => p.CanWrite))
        {
            var path = prefix + property.Name;
            var value = property.GetValue(dto);
            if (value == null)
            {
                yield return path;
            }
            else if (property.PropertyType.Namespace == typeof(ObservationSearchFilterDto).Namespace)
            {
                foreach (var nested in UnsetProperties(value, path + "."))
                    yield return nested;
            }
        }
    }
}
