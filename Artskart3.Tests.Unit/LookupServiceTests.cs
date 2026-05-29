using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class LookupServiceTests
{
    private readonly Mock<ILookupRepository> _repositoryMock;
    private readonly LookupService _sut;

    public LookupServiceTests()
    {
        _repositoryMock = new Mock<ILookupRepository>();
        _sut = new LookupService(_repositoryMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new LookupService(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("lookupRepository");
    }

    [Fact]
    public async Task GetCategoriesAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(Enumerable.Empty<CategoryTypeDto>());

        await _sut.GetCategoriesAsync();

        _repositoryMock.Verify(r => r.GetCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsResultFromRepository()
    {
        var expected = new List<CategoryTypeDto>
        {
            new()
            {
                Id = 1,
                Name = "Rødliste",
                Categories = [new CategoryDto { Id = 10, Code = "CR", Name = "Critically Endangered" }]
            }
        };
        _repositoryMock.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(expected);

        var result = await _sut.GetCategoriesAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(Enumerable.Empty<CategoryTypeDto>());

        var result = await _sut.GetCategoriesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAreasAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetAreasAsync()).ReturnsAsync(Enumerable.Empty<AreaTypeDto>());

        await _sut.GetAreasAsync();

        _repositoryMock.Verify(r => r.GetAreasAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAreasAsync_ReturnsResultFromRepository()
    {
        var expected = new List<AreaTypeDto>
        {
            new()
            {
                Id = 1,
                Name = "Fylke",
                Areas = [new AreaDto { Id = 10, Fid = "03", Name = "Oslo", IsCurrent = true, ObservationCount = 500 }]
            }
        };
        _repositoryMock.Setup(r => r.GetAreasAsync()).ReturnsAsync(expected);

        var result = await _sut.GetAreasAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAreasAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetAreasAsync()).ReturnsAsync(Enumerable.Empty<AreaTypeDto>());

        var result = await _sut.GetAreasAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstitutionsAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetInstitutionsAsync()).ReturnsAsync(Enumerable.Empty<InstitutionDto>());

        await _sut.GetInstitutionsAsync();

        _repositoryMock.Verify(r => r.GetInstitutionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetInstitutionsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<InstitutionDto>
        {
            new() { Id = 1, Name = "Naturhistorisk museum", Code = "NHM", ObservationCount = 1000 }
        };
        _repositoryMock.Setup(r => r.GetInstitutionsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetInstitutionsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetInstitutionsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetInstitutionsAsync()).ReturnsAsync(Enumerable.Empty<InstitutionDto>());

        var result = await _sut.GetInstitutionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxonGroupsAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetTaxonGroupsAsync()).ReturnsAsync(Enumerable.Empty<TaxonGroupDto>());

        await _sut.GetTaxonGroupsAsync();

        _repositoryMock.Verify(r => r.GetTaxonGroupsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetTaxonGroupsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<TaxonGroupDto>
        {
            new() { Id = 1, Name = "Fugler", ObservationCount = 2000 }
        };
        _repositoryMock.Setup(r => r.GetTaxonGroupsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetTaxonGroupsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetTaxonGroupsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetTaxonGroupsAsync()).ReturnsAsync(Enumerable.Empty<TaxonGroupDto>());

        var result = await _sut.GetTaxonGroupsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBehaviorsAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetBehaviorsAsync()).ReturnsAsync(Enumerable.Empty<BehaviorDto>());

        await _sut.GetBehaviorsAsync();

        _repositoryMock.Verify(r => r.GetBehaviorsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetBehaviorsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<BehaviorDto>
        {
            new() { Id = 1, Name = "Hekking", Variants = "Hekking;Breeding", ObservationCount = 300, Description = "Hekkeatferd" }
        };
        _repositoryMock.Setup(r => r.GetBehaviorsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetBehaviorsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetBehaviorsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetBehaviorsAsync()).ReturnsAsync(Enumerable.Empty<BehaviorDto>());

        var result = await _sut.GetBehaviorsAsync();

        result.Should().BeEmpty();
    }
}
