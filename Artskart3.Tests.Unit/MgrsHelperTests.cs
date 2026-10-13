using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Helpers;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class MgrsHelperTests
{
    [Theory]
    [InlineData("33VTG62004300")]
    [InlineData("33vtg62004300")]
    [InlineData("33VTG6243")]
    [InlineData("33VTG62")]
    [InlineData("33VTG6200043000")]
    public void IsValidMgrsNotation_godtar_gyldige_former(string notasjon)
    {
        MgrsHelper.IsValidMgrsNotation(notasjon).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("33VTG62004")]        // ulikt antall sifre for easting og northing
    [InlineData("3VTG6243")]          // bare ett sonesiffer
    [InlineData("33VIG6243")]         // I finnes ikke i MGRS-alfabetet
    [InlineData("33VOG6243")]         // O finnes ikke i MGRS-alfabetet
    [InlineData("33VTG")]             // ingen sifre
    [InlineData("33VTG62004300X")]
    [InlineData("tull")]
    public void IsValidMgrsNotation_avviser_ugyldige_former(string? notasjon)
    {
        MgrsHelper.IsValidMgrsNotation(notasjon).Should().BeFalse();
    }

    /// <summary>
    /// 33VTG62004300 ligger i Oslo: UTM-sone 33, 100 km-ruten TG, og 62000/43000
    /// inne i ruten med ti meters oppløsning. Northingen er den interessante delen —
    /// MGRS oppgir bare posisjonen innenfor et 2 000 km-bånd, og båndet velges ut fra
    /// midtbreddegraden til sonebåndbokstaven V.
    /// </summary>
    [Fact]
    public void MgrsToUtm_tolker_en_norsk_rutereferanse()
    {
        var resultat = MgrsHelper.MgrsToUtm("33VTG62004300");

        resultat.Should().NotBeNull();
        resultat!.Point.X.Should().Be(262000);
        resultat.Point.Y.Should().Be(6643000);
        resultat.Point.Epsg.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        resultat.CoordinatePrecisionInMeters.Should().Be(7);
    }

    [Fact]
    public void MgrsToUtm_tolker_sma_bokstaver_likt()
    {
        var store = MgrsHelper.MgrsToUtm("33VTG62004300");
        var små = MgrsHelper.MgrsToUtm("33vtg62004300");

        små.Should().BeEquivalentTo(store);
    }

    /// <summary>
    /// Presisjonsverdiene er Artskart 2s 7-tallsrekke, ikke rutestørrelsen. De er et
    /// spor av at presisjonen kom fra MGRS og ikke fra kilden, og de ligger i
    /// eksisterende rader.
    /// </summary>
    [Theory]
    [InlineData("33VTG6200043000", 1)]
    [InlineData("33VTG62004300", 7)]
    [InlineData("33VTG620430", 77)]
    [InlineData("33VTG6243", 777)]
    [InlineData("33VTG64", 7777)]
    public void MgrsToUtm_setter_presisjon_etter_antall_sifre(string notasjon, int forventet)
    {
        MgrsHelper.MgrsToUtm(notasjon)!.CoordinatePrecisionInMeters.Should().Be(forventet);
    }

    [Theory]
    [InlineData("33VTG6200043000", 1)]
    [InlineData("33VTG62004300", 10)]
    [InlineData("33VTG620430", 100)]
    [InlineData("33VTG6243", 1000)]
    [InlineData("33VTG64", 10000)]
    public void TileSize_er_hundre_kilometer_delt_paa_ti_per_siffer(string notasjon, double forventet)
    {
        MgrsHelper.TileSize(notasjon).Should().Be(forventet);
    }

    /// <summary>
    /// Lavere oppløsning gir samme rutehjørne, bare grovere. Ruten TG starter på
    /// 200 000 øst og 6 600 000 nord.
    /// </summary>
    [Fact]
    public void MgrsToUtm_grovere_referanse_gir_rutens_hjoerne()
    {
        var resultat = MgrsHelper.MgrsToUtm("33VTG64");

        resultat!.Point.X.Should().Be(260000);
        resultat.Point.Y.Should().Be(6640000);
    }

    [Fact]
    public void MgrsToUtm_gir_null_for_en_rutebokstav_utenfor_alfabetet()
    {
        MgrsHelper.MgrsToUtm("33VIG6243").Should().BeNull();
    }

    [Fact]
    public void MgrsToUtm_gir_null_for_ugyldig_sone()
    {
        MgrsHelper.MgrsToUtm("99VTG6243").Should().BeNull();
    }

    /// <summary>
    /// Sone 29 har vi ingen definisjon for. Artskart 2 kaster her, og et unntak feller
    /// hele nodeimporten — her blir det én avvist post.
    /// </summary>
    [Theory]
    [InlineData("29VTG6243")]
    [InlineData("38VTG6243")]
    public void MgrsToUtm_gir_null_for_soner_utenfor_registeret(string notasjon)
    {
        MgrsHelper.MgrsToUtm(notasjon).Should().BeNull();
    }

    [Fact]
    public void MgrsToUtm_gir_null_for_ulikt_antall_sifre()
    {
        MgrsHelper.MgrsToUtm("33VTG62004").Should().BeNull();
    }
}
