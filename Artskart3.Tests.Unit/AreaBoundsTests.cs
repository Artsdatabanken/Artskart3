using Artskart3.Core.Application.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Boksgeometrien som områdelukingen hviler på.
///
/// Hele optimaliseringen bygger på at manglende overlapp er et BEVIS på at
/// området ikke kan bidra. Er Intersects feil i én retning, fjerner vi områder
/// som faktisk har treff, og svaret blir stille galt — ingen feilmelding, bare
/// færre lokasjoner på kartet.
/// </summary>
public class AreaBoundsTests
{
    private static readonly AreaBounds Midten = new(1000, 1000, 2000, 2000);

    [Fact]
    public void Overlappende_bokser_skjaerer_hverandre()
    {
        Midten.Intersects(new AreaBounds(1500, 1500, 3000, 3000)).Should().BeTrue();
    }

    [Fact]
    public void Boks_inni_en_annen_skjaerer()
    {
        Midten.Intersects(new AreaBounds(1200, 1200, 1300, 1300)).Should().BeTrue();
        new AreaBounds(1200, 1200, 1300, 1300).Intersects(Midten).Should().BeTrue();
    }

    [Theory]
    [InlineData(2500, 1000, 3000, 2000)] // helt til høyre
    [InlineData(0, 1000, 500, 2000)]     // helt til venstre
    [InlineData(1000, 2500, 2000, 3000)] // helt over
    [InlineData(1000, 0, 2000, 500)]     // helt under
    public void Adskilte_bokser_skjaerer_ikke(int minX, int minY, int maxX, int maxY)
    {
        Midten.Intersects(new AreaBounds(minX, minY, maxX, maxY)).Should().BeFalse();
    }

    /// <summary>
    /// Berøring teller som overlapp. Et område som akkurat tangerer utsnittskanten
    /// kan ha en lokasjon nøyaktig på grensen, og den skal være med.
    /// </summary>
    [Fact]
    public void Beroering_i_kanten_teller_som_overlapp()
    {
        Midten.Intersects(new AreaBounds(2000, 1000, 3000, 2000)).Should().BeTrue();
        Midten.Intersects(new AreaBounds(1000, 2000, 2000, 3000)).Should().BeTrue();
    }

    /// <summary>
    /// Adskilt i X, overlappende i Y — og omvendt. Dette er feilen man får hvis
    /// en av de fire sammenligningene bruker feil akse.
    /// </summary>
    [Fact]
    public void Overlapp_maa_gjelde_begge_akser()
    {
        Midten.Intersects(new AreaBounds(5000, 1500, 6000, 1600)).Should().BeFalse();
        Midten.Intersects(new AreaBounds(1500, 5000, 1600, 6000)).Should().BeFalse();
    }

    [Fact]
    public void Expand_utvider_likt_i_alle_retninger()
    {
        Midten.Expand(500).Should().Be(new AreaBounds(500, 500, 2500, 2500));
    }

    /// <summary>
    /// Marginen er hele poenget: en boks som så vidt ikke nådde fram, skal nå fram
    /// etter utvidelse. Det er den som dekker vinduet mellom to lastinger.
    /// </summary>
    [Fact]
    public void Margin_gjoer_at_naerliggende_bokser_skjaerer()
    {
        var naerme = new AreaBounds(2500, 1000, 3000, 2000);

        Midten.Intersects(naerme).Should().BeFalse();
        Midten.Expand(1000).Intersects(naerme).Should().BeTrue();
    }
}
