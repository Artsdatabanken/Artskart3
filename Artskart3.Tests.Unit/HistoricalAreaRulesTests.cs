using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tidsstemplene og Fid-ene er hentet fra Area i den lokale kopien av produksjonen.
/// </summary>
public class HistoricalAreaRulesTests
{
    private const int Kommune = (int)AreaType.Municipality;
    private const int Fylke = (int)AreaType.County;

    [Theory]
    [InlineData(Kommune, "1902", 2019, 1, 29, 2020)] // Tromsø, før regionreformen
    [InlineData(Kommune, "1504", 2018, 10, 30, 2020)] // Ålesund
    [InlineData(Kommune, "5030", 2018, 10, 30, 2020)] // Klæbu, nummerert i Trøndelag fra 2018
    [InlineData(Kommune, "5401", 2021, 1, 6, 2024)] // Tromsø i Troms og Finnmark
    [InlineData(Kommune, "1507", 2021, 1, 6, 2024)] // Ålesund, delt i 2024
    [InlineData(Kommune, "3002", 2020, 1, 2, 2024)] // Viken
    [InlineData(Fylke, "19", 2018, 10, 30, 2020)] // Troms
    [InlineData(Fylke, "18_2017", 2018, 10, 30, 2020)] // Nordland, grensen før Tjeldsund
    [InlineData(Fylke, "54", 2021, 1, 6, 2024)] // Troms og Finnmark
    public void Følger_inndelingen_importen_tilhører(int areaTypeId, string fid, int år, int måned, int dag, int sluttår)
    {
        HistoricalAreaRules.EndYear(areaTypeId, fid, new DateTime(år, måned, dag)).Should().Be(sluttår);
    }

    [Theory]
    [InlineData("16")] // Sør-Trøndelag
    [InlineData("17")] // Nord-Trøndelag
    public void Trøndelagsfylkene_sluttet_i_2018(string fid)
    {
        HistoricalAreaRules.EndYear(Fylke, fid, new DateTime(2018, 10, 30)).Should().Be(2018);
    }

    [Fact]
    public void Møre_og_Romsdals_grense_fra_2017_sluttet_da_Rindal_ble_flyttet()
    {
        HistoricalAreaRules.EndYear(Fylke, "15_2017", new DateTime(2018, 10, 30)).Should().Be(2019);
    }

    [Fact]
    public void Unntakene_gjelder_bare_fylker()
    {
        HistoricalAreaRules.EndYear(Kommune, "16", new DateTime(2018, 10, 30)).Should().Be(2020);
    }
}
