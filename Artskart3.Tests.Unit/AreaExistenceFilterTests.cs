using Artskart3.Core.Application.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vokter filtreringen av områdefilterets ELLER-grener.
///
/// Fylkes-ID-er slås opp mot både fylke og Svalbard, fordi et fylkesvalg på
/// Svalbard ellers ville falt ut. Men Svalbard har bare seks områder
/// (2101–2105, 2201) og ingen av dem deler ID med et fylke, så for et vanlig
/// fylke er Svalbard-grenen alltid tom.
///
/// Tom er ikke gratis: grenen gjør predikatet til en ELLER over to
/// områdetyper, og da kan ikke IX_OEI_AreaListView levere radene ferdig
/// datosortert. Målt på største fylke, 7 976 997 indeksrader: 2 ms med én
/// gren, 719 ms med to.
/// </summary>
public class AreaExistenceFilterTests
{
    private const int Fylke = 2;
    private const int Svalbard = 6;

    private static StubAreaHierarchyService MedOmraader(params (int Type, int Id)[] omraader)
    {
        var stub = new StubAreaHierarchyService();
        foreach (var (type, id) in omraader)
            stub.Bounds[(type, id)] = new AreaBounds(0, 0, 1000, 1000);
        return stub;
    }

    /// <summary>
    /// DEN VIKTIGSTE. Boksene lastes ved oppstart, og før første last vet vi
    /// ingenting. Tolkes «vet ikke» som «finnes ikke», forsvinner ALLE grener
    /// og områdefilteret returnerer stille tomt — et tomt kart uten
    /// feilmelding. Det er mye verre enn en treg spørring.
    /// </summary>
    [Fact]
    public void Uten_lastede_bokser_beholdes_alle_ider()
    {
        var tjeneste = new StubAreaHierarchyService();

        tjeneste.FilterToExistingAreas([3, 50, 2101], Svalbard).Should().Equal(3, 50, 2101);
    }

    [Fact]
    public void Fylkesid_som_ikke_finnes_som_svalbard_fjernes()
    {
        var tjeneste = MedOmraader((Fylke, 50), (Svalbard, 2101));

        tjeneste.FilterToExistingAreas([50], Svalbard).Should().BeEmpty();
    }

    [Fact]
    public void Svalbardid_beholdes_i_svalbardgrenen()
    {
        var tjeneste = MedOmraader((Fylke, 50), (Svalbard, 2101));

        tjeneste.FilterToExistingAreas([2101], Svalbard).Should().Equal(2101);
    }

    /// <summary>
    /// Begge grenene filtreres, ikke bare Svalbard. Velger brukeren et
    /// Svalbard-område, resolver Fid-en til en ID som finnes som type 6 men
    /// IKKE som fylke — da er det fylkesgrenen som er den tomme.
    /// </summary>
    [Fact]
    public void Svalbardid_fjernes_fra_fylkesgrenen()
    {
        var tjeneste = MedOmraader((Fylke, 50), (Svalbard, 2101));

        tjeneste.FilterToExistingAreas([2101], Fylke).Should().BeEmpty();
    }

    /// <summary>
    /// Et blandet utvalg skal dele seg riktig, ikke falle ned på én gren.
    /// </summary>
    [Fact]
    public void Blandet_utvalg_deles_mellom_grenene()
    {
        var tjeneste = MedOmraader((Fylke, 50), (Fylke, 3), (Svalbard, 2101));
        int[] valgt = [50, 3, 2101];

        tjeneste.FilterToExistingAreas(valgt, Fylke).Should().Equal(50, 3);
        tjeneste.FilterToExistingAreas(valgt, Svalbard).Should().Equal(2101);
    }

    [Fact]
    public void Tomt_utvalg_gir_tomt()
    {
        MedOmraader((Fylke, 50)).FilterToExistingAreas([], Fylke).Should().BeEmpty();
    }
}
