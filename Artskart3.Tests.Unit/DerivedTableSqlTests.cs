using System.Text.RegularExpressions;
using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class DerivedTableSqlTests
{
    private static string Steg(string navn) => DerivedTableSql.Steps.Single(x => x.Name.Contains(navn)).Sql;

    [Fact]
    public void Har_tre_steg()
    {
        DerivedTableSql.Steps.Should().HaveCount(3);
    }

    /// <summary>
    /// Slektslinjen må være skrevet før indeksen, fordi indeksens fire
    /// taksonrangkolonner leses derfra (plan 4.5, «4 → 3»).
    /// </summary>
    [Fact]
    public void Slektslinjen_kommer_for_indeksen()
    {
        var rekkefølge = DerivedTableSql.Steps.Select(x => x.Name).ToList();

        rekkefølge.IndexOf("ObservationTaxonHierarchy").Should()
            .BeLessThan(rekkefølge.IndexOf("ObservationEntityIndex"));
    }

    /// <summary>
    /// De tre utledede kolonnene på observasjonen leses av indeksen, så de må settes
    /// først.
    /// </summary>
    [Fact]
    public void Observasjonskolonnene_settes_forst()
    {
        DerivedTableSql.Steps[0].Name.Should().Contain("Observation");
        DerivedTableSql.Steps[0].Sql.Should().Contain("UPDATE o");
    }

    /// <summary>
    /// Alle setningene gjelder bare observasjonene i runden. Uten den avgrensningen
    /// ville en runde rørt hele tabellen.
    /// </summary>
    [Fact]
    public void Alle_setningene_avgrenses_til_runden()
    {
        foreach (var steg in DerivedTableSql.Steps)
        {
            steg.Sql.Should().Contain("#stg_Observation", $"{steg.Name} skal bare gjelde runden");
        }
    }

    [Fact]
    public void Bruker_ingen_parametere()
    {
        Regex.Matches(DerivedTableSql.FullScript, @"@\w+").Should().BeEmpty();
    }

    // --- Slektslinjen ------------------------------------------------------

    [Fact]
    public void Slektslinjen_har_alle_26_rangkolonnene()
    {
        var sql = Steg("ObservationTaxonHierarchy");

        var liste = Regex.Match(sql, @"INSERT INTO dbo\.ObservationTaxonHierarchy \(([^)]*)\)").Groups[1].Value;

        liste.Split(',').Select(x => x.Trim()).Should().HaveCount(27, "ObservationId pluss 26 ranger");
        liste.Should().Contain("KingdomTaxonId").And.Contain("NotSetTaxonId");
    }

    /// <summary>
    /// Rangene 1–26 står i kolonnerekkefølge: <c>TaxonRank.Id</c> 1 er kingdom og 26 er
    /// notSet. Bytter de plass, havner artsnavnet i rikekolonnen.
    /// </summary>
    [Fact]
    public void Rangene_star_i_rekkefolge_fra_1_til_26()
    {
        var sql = Steg("ObservationTaxonHierarchy");

        var ranger = Regex.Matches(sql, @"forfader\.TaxonRankId = (\d+)")
            .Select(x => int.Parse(x.Groups[1].Value))
            .ToList();

        ranger.Should().Equal(Enumerable.Range(1, 26));
    }

    [Fact]
    public void Slektslinjen_slettes_for_den_settes_inn()
    {
        var sql = Steg("ObservationTaxonHierarchy");

        sql.IndexOf("DELETE", StringComparison.Ordinal).Should()
            .BeLessThan(sql.IndexOf("INSERT", StringComparison.Ordinal));
    }

    [Fact]
    public void Slektslinjen_hopper_over_slettede_observasjoner()
    {
        Steg("ObservationTaxonHierarchy").Should().Contain("o.IsDeleted = 0");
    }

    [Fact]
    public void Slektslinjen_splitter_hierarkistrengen_pa_komma()
    {
        Steg("ObservationTaxonHierarchy").Should().Contain("STRING_SPLIT(t.TaxonIdHiarchy, ',')");
    }

    // --- Entitetsindeksen --------------------------------------------------

    /// <summary>
    /// Indeksen har en columnstore-indeks, og en <c>UPDATE</c> der etterlater den gamle
    /// raden i delete-bitmappen til neste rebuild (plan 3.5).
    /// </summary>
    [Fact]
    public void Indeksen_slettes_og_settes_inn_igjen()
    {
        var sql = Steg("ObservationEntityIndex");

        sql.Should().Contain("DELETE i").And.Contain("INSERT INTO dbo.ObservationEntityIndex");
        sql.Should().NotContain("UPDATE");
    }

    [Fact]
    public void Indeksen_far_alle_nitten_kolonnene()
    {
        var liste = Regex.Match(
            Steg("ObservationEntityIndex"),
            @"INSERT INTO dbo\.ObservationEntityIndex \(([^)]*)\)",
            RegexOptions.Singleline).Groups[1].Value;

        liste.Split(',').Select(x => x.Trim()).Should().HaveCount(19);
    }

    /// <summary>
    /// Bare gjeldende områder. Et historisk fylke skal ikke gjøre en observasjon
    /// søkbar på en inndeling som ikke finnes lenger.
    /// </summary>
    [Fact]
    public void Indeksen_tar_bare_gjeldende_omrader()
    {
        Steg("ObservationEntityIndex").Should().Contain("a.IsCurrent = 1");
    }

    /// <summary>
    /// <c>Fid</c> til tall har to særtilfeller: verneområder (type 3) har prefikset
    /// «Naturbase VV», og historiske fylkes-Fid-er har understrek.
    /// </summary>
    [Fact]
    public void Indeksen_konverterer_fid_til_tall_med_begge_sartilfellene()
    {
        var sql = Steg("ObservationEntityIndex");

        sql.Should().Contain("REPLACE(a.Fid, 'Naturbase VV', '')");
        sql.Should().Contain("REPLACE(a.Fid, '_', '')");
    }

    /// <summary>
    /// Institusjonsradene er erstattet av kolonnen <c>InstitutionOrgId</c>, og
    /// backfillen sletter alle 61 052 216 av dem. Å skrive dem på nytt ville vært
    /// arbeid for å ende i samme tilstand.
    /// </summary>
    [Fact]
    public void Indeksen_setter_ikke_inn_institusjonsrader()
    {
        Steg("ObservationEntityIndex").Should().NotContain("101");
    }

    [Fact]
    public void Indeksen_utleder_maaneden_av_innsamlingsdatoen()
    {
        Steg("ObservationEntityIndex").Should().Contain("MONTH(o.DateTimeCollected)");
    }

    [Fact]
    public void Indeksen_leser_taksonrangene_fra_slektslinjen()
    {
        var sql = Steg("ObservationEntityIndex");

        sql.Should().Contain("LEFT JOIN dbo.ObservationTaxonHierarchy h");
        sql.Should().Contain("h.SpeciesTaxonId").And.Contain("h.OrderTaxonId");
    }

    // --- Observasjonskolonnene ---------------------------------------------

    /// <summary>
    /// 3 = ikke gjenfunnet (tagg 6) slår 2 = ikke funnet (tagg 5). Rekkefølgen er
    /// backfillens.
    /// </summary>
    [Fact]
    public void Registreringsstatus_lar_tagg_6_sla_tagg_5()
    {
        var sql = Steg("Observation (utledede");

        var seks = sql.IndexOf("TagId = 6", StringComparison.Ordinal);
        var fem = sql.IndexOf("TagId = 5", StringComparison.Ordinal);

        seks.Should().BeGreaterThan(0);
        seks.Should().BeLessThan(fem, "tagg 6 skal vurderes først");
    }

    [Fact]
    public void Observasjonskolonnene_setter_de_tre_utledede_feltene()
    {
        var sql = Steg("Observation (utledede");

        sql.Should().Contain("o.HasMediaFiles =")
            .And.Contain("o.RegistrationStatusId =")
            .And.Contain("o.BehaviorId =");
    }
}
