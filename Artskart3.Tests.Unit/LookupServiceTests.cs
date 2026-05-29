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
}
