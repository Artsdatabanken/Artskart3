using Artskart3.Core.Application.Validation;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class CadastralUnitIdTests
{
    [Theory]
    [InlineData("1101-1/2", "1101-1/2")]
    [InlineData(" 1101 - 1 / 2 ", "1101-1/2")]
    [InlineData("0301-007/02", "0301-7/2")]
    [InlineData("3413-325/2/1", "3413-325/2/1")]
    [InlineData("3413-6/501/0/2", "3413-6/501/0/2")]
    [InlineData("1101-1/2/0", "1101-1/2")]
    [InlineData("1101-1/2/0/0", "1101-1/2")]
    [InlineData("1101-1/2/3/0", "1101-1/2/3")]
    [InlineData("1101-1/0", "1101-1/0")]
    public void Normalize_ValidNumber_ReturnsCanonicalForm(string input, string expected)
    {
        CadastralUnitId.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1101-1")]
    [InlineData("1101-1/")]
    [InlineData("101-1/2")]
    [InlineData("1101/1/2")]
    [InlineData(@"1101-1\2")]
    [InlineData("1101-1/2/3/4/5")]
    [InlineData("1101-123456/1")]
    [InlineData("1101-\uFF11/2")]
    [InlineData("\u0661\u0661\u0660\u0661-1/2")]
    public void Normalize_InvalidNumber_ReturnsNull(string? input)
    {
        CadastralUnitId.Normalize(input).Should().BeNull();
    }
}
