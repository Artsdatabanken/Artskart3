using Artskart3.Core.Application.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Regelen som avgjør om en lokasjonsgeometri er en rutenettrute og skal skjules.
///
/// Den er delt mellom databasestien i SearchRepository og minnelageret i
/// PolygonLocationStore nettopp fordi de to må gi samme svar. Tar den feil, er
/// utfallet geometri som forsvinner fra kartet uten noen feilmelding — og
/// forskjellen mellom de to stiene ville vist seg som polygoner som dukker opp
/// eller blir borte avhengig av hvilken sti som svarte.
/// </summary>
public class LocationGeometryRulesTests
{
    [Theory]
    [InlineData("POLYGON((0 0, 0 10, 10 10, 10 0, 0 0))")]
    [InlineData("POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0))")]
    public void Polygon_med_fem_koordinatpar_er_rutenettrute(string wkt)
    {
        LocationGeometryRules.IsRectangularPolygon(wkt).Should().BeTrue();
    }

    [Theory]
    [InlineData("POLYGON((0 0, 0 10, 5 15, 10 10, 10 0, 0 0))")] // seks par
    [InlineData("POLYGON((0 0, 0 10, 10 0, 0 0))")]              // fire par
    public void Polygon_med_annet_antall_koordinatpar_er_ikke_rutenettrute(string wkt)
    {
        LocationGeometryRules.IsRectangularPolygon(wkt).Should().BeFalse();
    }

    /// <summary>
    /// Regelen teller koordinatpar etter ANDRE parentes. For MULTILINESTRING er
    /// det den første linja, ikke en ytre ring — en transekt eller elvestrekning
    /// med fem punkter ble derfor lest som rutenettrute og forsvant fra kartet.
    /// </summary>
    [Fact]
    public void Multilinestring_med_fem_punkter_i_foerste_linje_er_ikke_rutenettrute()
    {
        const string wkt = "MULTILINESTRING((0 0, 1 1, 2 2, 3 3, 4 4), (9 9, 8 8))";

        LocationGeometryRules.IsRectangularPolygon(wkt).Should().BeFalse();
    }

    [Fact]
    public void Linestring_er_ikke_rutenettrute()
    {
        LocationGeometryRules.IsRectangularPolygon("LINESTRING(0 0, 1 1, 2 2, 3 3, 4 4)")
            .Should().BeFalse();
    }

    /// <summary>
    /// Et multipolygon er en reell geografisk utstrekning uansett hvor mange
    /// punkter den første ringen har. Regelen gjelder bare POLYGON.
    /// </summary>
    [Fact]
    public void Multipolygon_er_ikke_rutenettrute()
    {
        const string wkt = "MULTIPOLYGON(((0 0, 0 10, 10 10, 10 0, 0 0)), ((20 20, 20 30, 30 30, 20 20)))";

        LocationGeometryRules.IsRectangularPolygon(wkt).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("POINT(10 20)")]
    [InlineData("POLYGON")]
    [InlineData("POLYGON((0 0, 0 10, 10 10, 10 0, 0 0")] // uten avsluttende parentes
    public void Tomt_eller_ufullstendig_wkt_er_ikke_rutenettrute(string? wkt)
    {
        LocationGeometryRules.IsRectangularPolygon(wkt).Should().BeFalse();
    }

    /// <summary>
    /// Sensitivitetsregelen. Maskeringsruta til et sensitivt funn ER et rektangel,
    /// men den er det eneste kartet har å vise for funnet, så den unntas
    /// rutenettregelen over.
    ///
    /// Suffikset gjenkjennes på LookupId og ikke på geometrien, så regelen må
    /// kunne stå alene — databasestien uttrykker den inline for at EF skal
    /// oversette den til SQL, minnelageret kaller denne.
    /// </summary>
    [Theory]
    [InlineData("1_2000_2000_4_sensitiv")]
    [InlineData("_sensitiv")]
    public void LookupId_med_sensitiv_suffiks_er_sensitiv_lokalitet(string lookupId)
    {
        LocationGeometryRules.ErSensitivLokalitet(lookupId).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1_2000_2000_4")]
    [InlineData("sensitiv_1_2000")]      // suffikset må stå til slutt, ikke hvor som helst
    [InlineData("1_2000_SENSITIV")]      // store bokstaver er en annen streng
    public void Andre_lookupid_er_ikke_sensitiv_lokalitet(string? lookupId)
    {
        LocationGeometryRules.ErSensitivLokalitet(lookupId).Should().BeFalse();
    }
}
