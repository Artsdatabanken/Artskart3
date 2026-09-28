using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Reglene for å luke bort områder som ikke kan nå kartutsnittet.
///
/// Alle feilene her er stille: for aggressiv luking gir færre lokasjoner på
/// kartet uten en eneste feilmelding. Derfor tester hver regel begge retninger —
/// at vi fjerner det vi skal, og at vi IKKE fjerner noe annet.
/// </summary>
public class AreaBoundsPrunerTests
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;
    private const int Svalbard = (int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen;

    /// <summary>Oslo-aktig utsnitt.</summary>
    private static readonly AreaBounds Utsnitt = new(240_000, 6_630_000, 280_000, 6_670_000);

    private static Dictionary<(int, int), AreaBounds> Bokser(
        params (int Type, int Id, AreaBounds Boks)[] rader)
        => rader.ToDictionary(r => (r.Type, r.Id), r => r.Boks);

    [Fact]
    public void Omraade_utenfor_utsnittet_fjernes()
    {
        // Farsund ligger i sør, langt fra Oslo.
        var bokser = Bokser((Kommune, 4206, new AreaBounds(370_000, 6_420_000, 420_000, 6_474_000)));

        AreaBoundsPruner.Prune([4206], Utsnitt, bokser, Kommune).Should().BeEmpty();
    }

    [Fact]
    public void Omraade_inni_utsnittet_beholdes()
    {
        var bokser = Bokser((Kommune, 301, new AreaBounds(250_000, 6_640_000, 270_000, 6_660_000)));

        AreaBoundsPruner.Prune([301], Utsnitt, bokser, Kommune).Should().Equal(301);
    }

    [Fact]
    public void Bare_de_som_ikke_naar_fram_fjernes()
    {
        var bokser = Bokser(
            (Kommune, 301, new AreaBounds(250_000, 6_640_000, 270_000, 6_660_000)),   // Oslo
            (Kommune, 4206, new AreaBounds(370_000, 6_420_000, 420_000, 6_474_000)),  // Farsund
            (Kommune, 5029, new AreaBounds(255_000, 7_020_000, 273_000, 7_041_000))); // Skaun

        AreaBoundsPruner.Prune([301, 4206, 5029], Utsnitt, bokser, Kommune)
            .Should().Equal(301);
    }

    /// <summary>
    /// Fylkes-ID-er slås opp mot både fylke og Svalbard. Når type 2 ikke finnes,
    /// men type 6 når fram, må ID-en bli stående — ellers forsvinner Svalbard fra
    /// kartet ved et fylkesvalg.
    /// </summary>
    [Fact]
    public void ID_beholdes_hvis_en_av_typene_naar_fram()
    {
        var svalbardUtsnitt = new AreaBounds(400_000, 8_600_000, 600_000, 8_750_000);
        var bokser = Bokser(
            (Fylke, 21, new AreaBounds(240_000, 6_630_000, 260_000, 6_650_000)),      // treffer ikke
            (Svalbard, 21, new AreaBounds(430_000, 8_650_000, 560_000, 8_720_000)));  // treffer

        AreaBoundsPruner.Prune([21], svalbardUtsnitt, bokser, Fylke, Svalbard)
            .Should().Equal(21);
    }

    [Fact]
    public void ID_fjernes_naar_ingen_av_typene_naar_fram()
    {
        var bokser = Bokser(
            (Fylke, 21, new AreaBounds(900_000, 7_700_000, 1_000_000, 7_800_000)),
            (Svalbard, 21, new AreaBounds(430_000, 8_650_000, 560_000, 8_720_000)));

        AreaBoundsPruner.Prune([21], Utsnitt, bokser, Fylke, Svalbard).Should().BeEmpty();
    }

    /// <summary>
    /// Ukjent ID kan være nyimportert og ennå ikke med i oppslaget. Å fjerne den
    /// ville skjult ekte treff til neste lasting.
    /// </summary>
    [Fact]
    public void Ukjent_ID_beholdes()
    {
        var bokser = Bokser((Kommune, 301, new AreaBounds(250_000, 6_640_000, 270_000, 6_660_000)));

        AreaBoundsPruner.Prune([9999], Utsnitt, bokser, Kommune).Should().Equal(9999);
    }

    [Fact]
    public void Ukjent_ID_beholdes_sammen_med_en_som_fjernes()
    {
        var bokser = Bokser((Kommune, 4206, new AreaBounds(370_000, 6_420_000, 420_000, 6_474_000)));

        AreaBoundsPruner.Prune([4206, 9999], Utsnitt, bokser, Kommune).Should().Equal(9999);
    }

    /// <summary>
    /// Den farligste feilen: tjenesten har ikke rukket å laste. Uten denne regelen
    /// ville hvert eneste områdefilter blitt tømt og alle søk svart tomt.
    /// </summary>
    [Fact]
    public void Tomt_oppslag_luker_ingenting()
    {
        var tomt = new Dictionary<(int, int), AreaBounds>();

        AreaBoundsPruner.Prune([4206, 301, 5029], Utsnitt, tomt, Kommune)
            .Should().Equal(4206, 301, 5029);
    }

    [Fact]
    public void Ingen_omraadetyper_oppgitt_luker_ingenting()
    {
        var bokser = Bokser((Kommune, 4206, new AreaBounds(370_000, 6_420_000, 420_000, 6_474_000)));

        AreaBoundsPruner.Prune([4206], Utsnitt, bokser).Should().Equal(4206);
    }

    [Fact]
    public void Tom_inndata_gir_tom_utdata()
    {
        var bokser = Bokser((Kommune, 301, new AreaBounds(250_000, 6_640_000, 270_000, 6_660_000)));

        AreaBoundsPruner.Prune([], Utsnitt, bokser, Kommune).Should().BeEmpty();
    }

    /// <summary>
    /// En feilregistrert koordinat blåser opp boksen for hele området. Målt i
    /// prodlik database gjelder det 15 av 357 kommuner — Larvik har polygon på
    /// 30 × 83 km, men lokasjoner over 695 × 1323 km. Da skal luking IKKE skje:
    /// svaret blir riktig, bare ikke raskere.
    /// </summary>
    [Fact]
    public void Oppblaast_boks_luker_ikke()
    {
        var larvik = Bokser((Kommune, 3909, new AreaBounds(-60_000, 6_400_000, 635_000, 7_723_000)));

        AreaBoundsPruner.Prune([3909], Utsnitt, larvik, Kommune).Should().Equal(3909);
    }

    /// <summary>
    /// Rekkefølgen skal være uendret. Den havner i en IN-liste, og EF lager én
    /// navngitt parameter per element — stokkes den om, bommer plan-cachen.
    /// </summary>
    [Fact]
    public void Rekkefoelgen_bevares()
    {
        var innenfor = new AreaBounds(250_000, 6_640_000, 270_000, 6_660_000);
        var bokser = Bokser(
            (Kommune, 5, innenfor), (Kommune, 3, innenfor), (Kommune, 9, innenfor));

        AreaBoundsPruner.Prune([5, 3, 9], Utsnitt, bokser, Kommune).Should().Equal(5, 3, 9);
    }
}
