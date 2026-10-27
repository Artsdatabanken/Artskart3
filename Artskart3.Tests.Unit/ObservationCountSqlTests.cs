using System.Text.RegularExpressions;
using Artskart3.Infrastructure.Maintenance;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ObservationCountSqlTests
{
    private static string Sql(string tabell) =>
        ObservationCountSql.Targets.Single(x => x.Table == tabell).Sql;

    [Fact]
    public void Dekker_de_atte_tabellene_artskart_2_oppdaterer()
    {
        ObservationCountSql.Targets.Select(x => x.Table).Should().BeEquivalentTo(
            "Area", "BasisOfRecord", "Category", "Organization",
            "Behavior", "Tag", "TaxonGroup", "Taxon");
    }

    /// <summary>
    /// Hver prosedyre nullstiller kolonnen før den fyller den, så en rad uten
    /// observasjoner ender med <c>NULL</c> og ikke med gammelt tall. Uten
    /// nullstillingen ville et område som har mistet sine observasjoner beholdt
    /// tellingen fra forrige kjøring.
    /// </summary>
    [Fact]
    public void Nullstiller_alltid_for_den_fyller()
    {
        foreach (var mål in ObservationCountSql.Targets)
        {
            var nullstilling = mål.Sql.IndexOf("= NULL", StringComparison.Ordinal);
            var fylling = mål.Sql.IndexOf("ObservationCount = t.Antall", StringComparison.Ordinal);

            nullstilling.Should().BeGreaterThan(-1, $"{mål.Table} skal nullstille først");

            if (fylling > -1) nullstilling.Should().BeLessThan(fylling);
        }
    }

    /// <summary>
    /// Setningene kjøres uten parametere, så hver @-variabel må være deklarert i samme
    /// batch. En udeklarert variabel er en kjøretidsfeil midt i etterarbeidet.
    /// </summary>
    [Fact]
    public void Hver_variabel_er_deklarert_i_setningen()
    {
        foreach (var mål in ObservationCountSql.Targets)
        {
            mål.Sql.Should().NotBeEmpty();

            var brukt = Regex.Matches(mål.Sql, @"@\w+")
                .Select(x => x.Value)
                .Distinct()
                .ToList();

            foreach (var variabel in brukt)
            {
                mål.Sql.Should().Contain($"DECLARE {variabel}",
                    $"{mål.Table} bruker {variabel}");
            }

            // Taksonsetningen har @rang; de sju andre har ingen variabler i det hele
            // tatt. Finner testen ingen, har den ikke bevist noe — da er regexen gal.
            if (mål.Table == "Taxon") brukt.Should().Equal("@rang");
        }
    }

    // --- De enkelte tabellene ----------------------------------------------

    /// <summary>
    /// Artskart 2 joiner direkte på <c>LocationId</c>, uten å gå via
    /// <c>Location</c>. Resultatet er det samme, og det er én join mindre over 61 M
    /// rader.
    /// </summary>
    [Fact]
    public void Omrader_telles_via_locationareas()
    {
        Sql("Area").Should().Contain("INNER JOIN dbo.LocationAreas la ON la.LocationId = o.LocationId");
    }

    [Theory]
    [InlineData("Category", "CategoryId IS NOT NULL")]
    [InlineData("TaxonGroup", "TaxonGroupId IS NOT NULL")]
    public void Tabellene_med_nullbar_nokkel_filtrerer_bort_null(string tabell, string filter)
    {
        Sql(tabell).Should().Contain(filter);
    }

    /// <summary>
    /// <c>OrganizationRelation</c> finnes ikke i Artskart 3. De tre kildene som
    /// erstattet den må alle være med, ellers blir tellingen for lav.
    /// </summary>
    [Fact]
    public void Organisasjoner_telles_fra_alle_tre_kildene()
    {
        var sql = Sql("Organization");

        sql.Should().Contain("o.InstitutionOrgId")
            .And.Contain("o.DatasetOrgId")
            .And.Contain("p.ProjectOrgId");
        sql.Should().NotContain("OrganizationRelation");
    }

    /// <summary>
    /// En organisasjon som er både utgiver og datasett for samme post skal telle én
    /// gang — <c>UNION</c>, ikke <c>UNION ALL</c>.
    /// </summary>
    [Fact]
    public void Organisasjoner_telles_en_gang_per_observasjon()
    {
        var sql = Sql("Organization");

        sql.Should().Contain("UNION");
        sql.Should().NotContain("UNION ALL");
    }

    // --- Taksonene ---------------------------------------------------------

    [Fact]
    public void Taksoner_far_alle_seks_kolonnene_nullstilt()
    {
        var sql = Sql("Taxon");

        foreach (var kolonne in new[]
                 {
                     "ObservationCount", "WeeklyObservationCount", "DailyObservationCount",
                     "CumulativeObservationCount", "WeeklyCumulativeObservationCount",
                     "DailyCumulativeObservationCount"
                 })
        {
            sql.Should().Contain(kolonne);
        }
    }

    /// <summary>
    /// Uke og døgn regnes fra innsamlingsdatoen, ikke fra importtidspunktet: et
    /// datasett med gamle funn gir 0 uansett hvor nylig det kom inn.
    /// </summary>
    [Fact]
    public void Uke_og_dogn_regnes_fra_innsamlingsdatoen()
    {
        var sql = Sql("Taxon");

        sql.Should().Contain("DateTimeCollected >= DATEADD(DAY, -7,");
        sql.Should().Contain("DateTimeCollected >= DATEADD(DAY, -1,");
    }

    /// <summary>
    /// Artskart 2 bruker <c>GETDATE()</c>, altså serverens lokale tid. Artskart 3
    /// bruker UTC ellers, og på et sju dagers vindu er forskjellen uten betydning.
    /// </summary>
    [Fact]
    public void Tiden_er_utc()
    {
        var sql = Sql("Taxon");

        sql.Should().Contain("SYSUTCDATETIME()");
        sql.Should().NotContain("GETDATE()");
    }

    /// <summary>
    /// Den kumulative løkken må gå nedover: et takson summerer barnas kumulative tall,
    /// så barna må være ferdige først.
    /// </summary>
    [Fact]
    public void Den_kumulative_lokken_gar_nedover_mot_rot()
    {
        var sql = Sql("Taxon");

        sql.Should().Contain($"DECLARE @rang INT = {ObservationCountSql.CumulativeStartRank}");
        sql.Should().Contain("WHILE @rang >= 1");
        sql.Should().Contain("SET @rang = @rang - 1");
    }

    /// <summary>
    /// Artskart 2 starter på 24, men rangene går til 26 — så taksoner på rang 25
    /// (<c>form</c>) og 26 (<c>notSet</c>) får aldri kumulative tall, og bidrar ikke
    /// oppover. Målt: 807 observasjoner ligger på et rang-25-takson.
    ///
    /// Konstanten er med for at avviket skal være synlig og ett sted — ikke fordi 24
    /// er riktig.
    /// </summary>
    [Fact]
    public void Startrangen_er_artskart_2s_og_dokumentert_som_for_lav()
    {
        ObservationCountSql.CumulativeStartRank.Should().Be(24);
    }
}
