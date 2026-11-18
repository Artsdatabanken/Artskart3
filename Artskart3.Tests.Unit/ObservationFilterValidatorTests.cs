using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Constants;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ObservationFilterValidatorTests
{
    [Fact]
    public void GetError_WithValidCadastralUnitIds_ReturnsNull()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2", "0301-207/1"] };

        ObservationFilterValidator.GetError(filter).Should().BeNull();
    }

    [Fact]
    public void GetError_WithInvalidCadastralUnitId_NamesTheId()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2", "1101-1"] };

        ObservationFilterValidator.GetError(filter).Should().Contain("'1101-1'");
    }

    [Fact]
    public void GetError_WithNullCadastralUnitId_ReturnsError()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = [null!] };

        ObservationFilterValidator.GetError(filter).Should().NotBeNull();
    }

    [Fact]
    public void GetError_WithTooManyCadastralUnitIds_ReturnsError()
    {
        var filter = new ObservationSearchFilterDto
        {
            CadastralUnitIds = Enumerable.Range(1, SearchConstants.MaxCadastralUnitFilterSize + 1)
                .Select(n => $"1101-{n}/1")
                .ToArray()
        };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.CadastralUnitIds));
    }
}
