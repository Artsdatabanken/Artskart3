using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportValueHelperTests
{
    private readonly List<ImportIssue> _issues = [];

    private int? Tolk(string? input) =>
        ImportValueHelper.ExtractIntegerIfPossible(input, "coordinateUncertaintyInMeters", _issues);

    [Theory]
    [InlineData("10", 10)]
    [InlineData("10 meter", 10)]
    [InlineData("10 meters", 10)]
    [InlineData("10m", 10)]
    [InlineData("ca 50", 50)]
    [InlineData("CA 50", 50)]
    [InlineData("  25  ", 25)]
    public void ExtractIntegerIfPossible_tolker_vanlige_former(string input, int forventet)
    {
        Tolk(input).Should().Be(forventet);
        _issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData("12.7", 12)]
    [InlineData("12,7", 12)]
    [InlineData("12.2", 12)]
    public void ExtractIntegerIfPossible_runder_ned_uansett_desimalskilletegn(string input, int forventet)
    {
        Tolk(input).Should().Be(forventet);
        _issues.Should().BeEmpty();
    }

    /// <summary>
    /// En presisjon på en halv meter er en presisjon, ikke «ingen». Derfor rundes
    /// intervallet (0, 1) opp.
    /// </summary>
    [Theory]
    [InlineData("0.5")]
    [InlineData("0,5")]
    [InlineData("0.001")]
    public void ExtractIntegerIfPossible_runder_verdier_under_en_opp_til_en(string input)
    {
        Tolk(input).Should().Be(1);
        _issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.234,56")]
    public void ExtractIntegerIfPossible_gir_merknad_for_verdier_som_ikke_kan_brukes(string input)
    {
        Tolk(input).Should().BeNull();

        _issues.Should().HaveCount(1);
        _issues[0].Term.Should().Be("coordinateUncertaintyInMeters");
        _issues[0].Original.Should().Be(input);
        _issues[0].Remark.Should().Be(ImportIssueRemark.NoValidInteger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractIntegerIfPossible_gir_ingen_merknad_for_tomt_felt(string? input)
    {
        Tolk(input).Should().BeNull();
        _issues.Should().BeEmpty();
    }

    /// <summary>
    /// Alternativet <c>m</c> i regexen treffer enhver m, så «metres» mister sin m og
    /// resten blir uparsbart. Dagens oppførsel, dokumentert med en test slik at en
    /// framtidig endring blir et valg og ikke et uhell.
    /// </summary>
    [Fact]
    public void ExtractIntegerIfPossible_klarer_ikke_metres()
    {
        Tolk("2 metres").Should().BeNull();
        _issues.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(59.123456, 59.123456)]
    [InlineData(59.1234567, 59.123457)]
    [InlineData(10.0, 10.0)]
    [InlineData(-5.55555555, -5.555556)]
    public void SetDigitsForLatLon_runder_til_seks_desimaler(double input, double forventet)
    {
        ImportValueHelper.SetDigitsForLatLon(input).Should().Be(forventet);
    }

    [Fact]
    public void Left_tar_de_foerste_tegnene()
    {
        ImportValueHelper.Left("abcdef", 3).Should().Be("abc");
        ImportValueHelper.Left("ab", 5).Should().Be("ab");
        ImportValueHelper.Left("", 5).Should().Be("");
    }

    /// <summary>
    /// <c>null</c> inn skal gi <c>null</c> ut, ikke tom streng. Forskjellen avgjør om
    /// nedstrøms sjekker ser feltet som utfylt.
    /// </summary>
    [Fact]
    public void Left_gir_null_for_null()
    {
        ImportValueHelper.Left(null, 3).Should().BeNull();
        ImportValueHelper.NonSpaceLeft(null, 3).Should().BeNull();
    }

    [Fact]
    public void NonSpaceLeft_fjerner_mellomrom_foer_den_kutter()
    {
        ImportValueHelper.NonSpaceLeft("a b c d", 3).Should().Be("abc");
        ImportValueHelper.NonSpaceLeft(" ab ", 10).Should().Be("ab");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void JsonSanityFixes_rorer_ikke_tomme_verdier(string? input)
    {
        ImportValueHelper.JsonSanityFixes(input).Should().Be(input);
    }

    [Fact]
    public void JsonSanityFixes_rorer_ikke_gyldig_json()
    {
        const string json = """{"a":"1"}""";

        ImportValueHelper.JsonSanityFixes(json).Should().Be(json);
    }

    /// <summary>
    /// Formen Artskart 2 reparerer: objektet mangler anførselstegnet etter
    /// krøllparentesen, men har ett på slutten.
    /// </summary>
    [Fact]
    public void JsonSanityFixes_setter_inn_manglende_anfoerselstegn()
    {
        ImportValueHelper.JsonSanityFixes("{abc\":\"def\"").Should().Be("{\"abc\":\"def");
    }

    [Fact]
    public void JsonSanityFixes_dobler_bakstreker()
    {
        ImportValueHelper.JsonSanityFixes(@"{""sti"":""C:\temp""}")
            .Should().Be(@"{""sti"":""C:\\temp""}");
    }
}
