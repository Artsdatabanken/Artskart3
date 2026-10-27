using System.Text.RegularExpressions;
using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class PublishSqlTests
{
    private static string Steg(string navn) => PublishSql.Steps.Single(x => x.Name == navn).Sql;

    /// <summary>Kolonnelisten i den første <c>INSERT</c>-en i en setning.</summary>
    private static string InsertListe(string sql)
    {
        var treff = Regex.Match(sql, @"INSERT(?:\s+INTO)?\s+(?:dbo\.\w+\s*)?\(([^)]*)\)", RegexOptions.Singleline);

        treff.Success.Should().BeTrue("setningen skal ha en INSERT-liste");

        return treff.Groups[1].Value;
    }

    [Fact]
    public void Har_et_steg_per_tabell_pluss_kontrollen()
    {
        PublishSql.Steps.Should().HaveCount(ImportStagingSchema.All.Count + 1);
    }

    [Fact]
    public void Kontrollen_kommer_forst()
    {
        PublishSql.Steps[0].Name.Should().Be("Kontroll");
    }

    [Fact]
    public void Hver_tabell_i_kartet_har_et_steg()
    {
        var steg = PublishSql.Steps.Select(x => x.Name).ToList();

        foreach (var tabell in ImportStagingSchema.All)
        {
            steg.Should().Contain(tabell.TargetTable);
        }
    }

    /// <summary>
    /// Lokaliteten må finnes før observasjonen kan peke på den, og observasjonen før
    /// barna kan slås opp på Id-en.
    /// </summary>
    [Fact]
    public void Rekkefolgen_er_lokalitet_observasjon_barn()
    {
        var steg = PublishSql.Steps.Select(x => x.Name).ToList();

        steg.IndexOf("Location").Should().BeLessThan(steg.IndexOf("Observation"));
        steg.IndexOf("Observation").Should().BeLessThan(steg.IndexOf("ObservationDetails"));
        steg.IndexOf("Observation").Should().BeLessThan(steg.IndexOf("ObservationTags"));
        steg.IndexOf("Location").Should().BeLessThan(steg.IndexOf("LocationAreas"));
    }

    /// <summary>
    /// En parameter skriveren ikke setter gir en kjøretidsfeil midt i en runde. Finnes
    /// det bare to, er det to å huske.
    /// </summary>
    [Fact]
    public void Bruker_bare_de_to_parameterne_skriveren_setter()
    {
        var parametere = Regex.Matches(PublishSql.FullScript, @"@\w+")
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        parametere.Should().BeEquivalentTo([PublishSql.NowParameter, PublishSql.SridParameter]);
    }

    [Fact]
    public void Hvert_steg_navngir_tabellen_sin()
    {
        foreach (var tabell in ImportStagingSchema.All)
        {
            Steg(tabell.TargetTable).Should().Contain("dbo." + tabell.TargetTable);
        }
    }

    // --- Oversettelsene ----------------------------------------------------

    /// <summary>
    /// <c>LookupId</c> er ikke en kolonne på <c>Observation</c> — den er veien til
    /// <c>LocationId</c>.
    /// </summary>
    [Fact]
    public void Observasjonen_setter_inn_locationid_og_ikke_lookupid()
    {
        var liste = InsertListe(Steg("Observation"));

        liste.Should().Contain("[LocationId]");
        liste.Should().NotContain("[LookupId]");
    }

    [Fact]
    public void Observasjonen_loser_opp_lokaliteten_pa_lookupid()
    {
        Steg("Observation").Should()
            .Contain("COALESCE(stg.LocationId, l.Id)")
            .And.Contain("LEFT JOIN dbo.Location l ON l.LookupId = stg.LookupId");
    }

    /// <summary>
    /// Geometrien bygges av WKB-en staging bærer, med SRID-en som parameter.
    /// </summary>
    [Fact]
    public void Lokaliteten_bygger_geometri_av_wkb()
    {
        var sql = Steg("Location");

        sql.Should().Contain("geometry::STGeomFromWKB(GeometryWkb, @srid)");

        InsertListe(sql).Should().NotContain("[GeometryWkb]");
    }

    [Fact]
    public void Sensitive_data_bygger_ogsaa_geometri_av_wkb()
    {
        Steg("SensitiveObservationData").Should().Contain("geometry::STGeomFromWKB(s.GeometryWkb, @srid)");
    }

    [Theory]
    [InlineData("ObservationDetails")]
    [InlineData("ObservationTags")]
    [InlineData("ObservationTaxons")]
    [InlineData("ObservationBehaviors")]
    [InlineData("ObservationProject")]
    [InlineData("MediaFile")]
    [InlineData("ObservationError")]
    [InlineData("ObservationLink")]
    [InlineData("SensitiveObservationData")]
    public void Barnetabellene_setter_inn_observationid_og_ikke_proxyid(string tabell)
    {
        var liste = InsertListe(Steg(tabell));

        liste.Should().NotContain("ProxyId");
    }

    [Fact]
    public void Koblingen_til_en_annen_observasjon_slaas_opp_pa_proxyid()
    {
        Steg("ObservationLink").Should()
            .Contain("LEFT JOIN dbo.Observation lo ON lo.ProxyId = s.LinkedProxyId");
    }

    // --- Mediefilene -------------------------------------------------------

    /// <summary>
    /// <c>MediaFile</c> bærer den nedlastede filen og nedlastingskøens tilstand. Ble
    /// radene slettet og satt inn på nytt, ville hver reimport kastet alle bilder og
    /// lagt dem i kø igjen.
    /// </summary>
    [Fact]
    public void Mediefilene_oppdateres_og_slettes_ikke_ubetinget()
    {
        var sql = Steg("MediaFile");

        sql.Should().Contain("UPDATE m SET", "eksisterende rader skal oppdateres, ikke erstattes");
        sql.Should().Contain("WHERE NOT EXISTS", "bare rader som er borte fra kilden slettes");
    }

    [Fact]
    public void Mediefilene_mister_ikke_nedlastingstilstanden()
    {
        var oppdatering = Steg("MediaFile").Split("DELETE")[0];

        oppdatering.Should().NotContain("Image");
        oppdatering.Should().NotContain("Downloaded");
        oppdatering.Should().NotContain("InDownloadqueue");
        oppdatering.Should().NotContain("DownloadRetryCount");
    }

    [Fact]
    public void Nye_mediefiler_settes_inn_med_tom_nedlastingstilstand()
    {
        InsertListe(Steg("MediaFile")).Should()
            .Contain("Size").And.Contain("Downloaded").And.Contain("DownloadRetryCount");
    }

    // --- Revisjonskolonnene ------------------------------------------------

    [Theory]
    [InlineData("Location")]
    [InlineData("Observation")]
    [InlineData("ObservationDetails")]
    [InlineData("MediaFile")]
    [InlineData("ObservationError")]
    [InlineData("ObservationLink")]
    public void Tabellene_med_revisjonskolonner_far_dem_satt(string tabell)
    {
        Steg(tabell).Should().Contain("CreatedAt").And.Contain("UpdatedAt").And.Contain("IsDeleted");
    }

    /// <summary>
    /// Koblingstabellene og de sensitive dataene har ingen revisjonskolonner i basen.
    /// </summary>
    [Theory]
    [InlineData("LocationAreas")]
    [InlineData("ObservationTags")]
    [InlineData("ObservationTaxons")]
    [InlineData("ObservationBehaviors")]
    [InlineData("ObservationProject")]
    [InlineData("SensitiveObservationData")]
    public void Tabellene_uten_revisjonskolonner_far_dem_ikke(string tabell)
    {
        Steg(tabell).Should().NotContain("CreatedAt");
    }

    /// <summary>
    /// En post som kommer tilbake i cachen er ikke slettet lenger.
    /// </summary>
    [Fact]
    public void En_post_som_kommer_tilbake_blir_levende_igjen()
    {
        Steg("Observation").Should().Contain("t.IsDeleted = 0").And.Contain("t.DeletedAt = NULL");
    }

    // --- Koblingstabellene -------------------------------------------------

    /// <summary>
    /// Slettingen går på alle observasjonene i runden, ikke bare på de som har rader i
    /// staging: en post som har mistet den siste taggen sin skal også miste raden.
    /// </summary>
    [Theory]
    [InlineData("ObservationTags")]
    [InlineData("ObservationTaxons")]
    [InlineData("ObservationBehaviors")]
    [InlineData("ObservationProject")]
    public void Koblingstabellene_slettes_for_hele_runden(string tabell)
    {
        Steg(tabell).Should().Contain("INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId");
    }

    [Theory]
    [InlineData("ObservationTags")]
    [InlineData("ObservationTaxons")]
    [InlineData("ObservationBehaviors")]
    [InlineData("ObservationProject")]
    [InlineData("LocationAreas")]
    public void Koblingstabellene_setter_inn_distinkte_rader(string tabell)
    {
        Steg(tabell).Should().Contain("SELECT DISTINCT", "den sammensatte nøkkelen tåler ikke dubletter");
    }

    // --- Kontrollen --------------------------------------------------------

    [Fact]
    public void Kontrollen_stopper_poster_uten_identitet_og_dubletter()
    {
        var sql = Steg("Kontroll");

        sql.Should().Contain("ProxyId IS NULL");
        sql.Should().Contain("HAVING COUNT(*) > 1");
        sql.Should().Contain("THROW");
    }
}
