using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vokter når områdetellingen kan ankre på kommuneradene og rulle opp til
/// fylke i stedet for å ankre på fylkesraden og filtrere ned.
///
/// Feilretningen her er motsatt av <see cref="AreaCountGeoJoinRulesTests"/>:
/// ruller vi opp i et tilfelle der det ikke holder, faller observasjoner BORT
/// av svaret i stedet for å komme i tillegg. Begge deler er stille feil, så
/// testene legger vekt på når planen skal være null.
/// </summary>
public class AreaCountRollupRulesTests
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;
    private const int Havomraade = (int)ObservationIndexEntityType.OceanArea;

    private static int? FidTilId(string fid) =>
        int.TryParse(fid.Replace("_", ""), out var id) ? id : null;

    // Kommune 5001 og 5006 i fylke 50; kommune 4601 i fylke 46; 3101 i 31.
    private static string? Forelder(string kommuneFid) => kommuneFid switch
    {
        "5001" or "5006" => "50",
        "4601" => "46",
        "3101" => "31",
        _ => null,
    };

    private static Area Omr(int type, string fid) =>
        new() { AreaTypeId = type, Fid = fid, ParentFid = "rot" };

    private static Dictionary<int, int[]>? Plan(
        IReadOnlyList<Area> utdata, string[]? kommuner,
        bool fylke = false, bool hav = false, bool verne = false)
        => AreaCountRollupRules.LagOpprullingsplan(
            utdata, kommuner, fylke, hav, verne, FidTilId, Forelder);

    // --- GJELDER -------------------------------------------------------------

    [Fact]
    public void En_kommune_gir_plan_for_sitt_fylke()
    {
        var plan = Plan([Omr(Fylke, "50")], ["5001"]);

        plan.Should().NotBeNull();
        plan!.Should().ContainKey(50);
        plan[50].Should().Equal(5001);
    }

    /// <summary>
    /// Flere kommuner i samme fylke havner i samme spørring. Det er dette som
    /// gjør distinkt telling nødvendig — en observasjon på grensa har rad i to
    /// av dem.
    /// </summary>
    [Fact]
    public void Flere_kommuner_i_samme_fylke_samles()
    {
        var plan = Plan([Omr(Fylke, "50")], ["5001", "5006"]);

        plan.Should().NotBeNull();
        plan!.Keys.Should().Equal(50);
        plan[50].Should().BeEquivalentTo([5001, 5006]);
    }

    [Fact]
    public void Kommuner_i_to_fylker_gir_en_gruppe_per_fylke()
    {
        var plan = Plan([Omr(Fylke, "50"), Omr(Fylke, "46")], ["5001", "4601"]);

        plan.Should().NotBeNull();
        plan!.Should().HaveCount(2);
        plan[50].Should().Equal(5001);
        plan[46].Should().Equal(4601);
    }

    // --- GJELDER IKKE --------------------------------------------------------

    /// <summary>
    /// DEN VIKTIGSTE. Geo-predikatet er en ELLER, så et fylkesvalg slipper
    /// gjennom observasjoner utenfor de valgte kommunene. Ruller vi opp fra
    /// kommuneradene, faller de bort — svaret blir for LAVT, stille.
    /// </summary>
    [Fact]
    public void Fylkesvalg_i_tillegg_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], ["5001"], fylke: true).Should().BeNull();
    }

    /// <summary>Havområder er utdataceller selv og kan ikke utledes av kommuner.</summary>
    [Fact]
    public void Havomraadevalg_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], ["5001"], hav: true).Should().BeNull();
    }

    /// <summary>
    /// Verneområder nøster ikke i fylker — et reservat kan krysse fylkesgrenser.
    /// Det finnes ingen forelder å summere til.
    /// </summary>
    [Fact]
    public void Verneomraadevalg_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], ["5001"], verne: true).Should().BeNull();
    }

    [Fact]
    public void Uten_kommunevalg_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], null).Should().BeNull();
        Plan([Omr(Fylke, "50")], []).Should().BeNull();
    }

    /// <summary>
    /// Utdata er kommuner, ikke fylker — altså zoomnivå 2. Der gjelder
    /// delmengderegelen allerede, og opprulling ville vært feil nivå.
    /// </summary>
    [Fact]
    public void Kommuner_som_utdata_gir_ingen_plan()
    {
        Plan([Omr(Kommune, "5001")], ["5001"]).Should().BeNull();
    }

    [Fact]
    public void Havomraade_blant_utdatacellene_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50"), Omr(Havomraade, "13")], ["5001"]).Should().BeNull();
    }

    /// <summary>
    /// Et utdatafylke uten kommuner i planen ville fått en tom celle i stedet
    /// for sitt tall.
    /// </summary>
    [Fact]
    public void Utdatafylke_uten_kommuner_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50"), Omr(Fylke, "46")], ["5001"]).Should().BeNull();
    }

    /// <summary>
    /// Omvendt: en kommune hvis fylke ikke er blant utdatacellene betyr at vi
    /// har misforstått hvordan lista ble snevret.
    /// </summary>
    [Fact]
    public void Kommune_utenfor_utdatafylkene_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], ["5001", "4601"]).Should().BeNull();
    }

    [Fact]
    public void Kommune_uten_kjent_forelder_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "50")], ["9999"]).Should().BeNull();
    }

    [Fact]
    public void Utolkbar_fid_gir_ingen_plan()
    {
        Plan([Omr(Fylke, "Naturbase VV50")], ["5001"]).Should().BeNull();
    }

    [Fact]
    public void Tom_utdataliste_gir_ingen_plan()
    {
        Plan([], ["5001"]).Should().BeNull();
    }

    /// <summary>
    /// Over taket på to fylker taper opprullingen mot semijoinen, fordi den
    /// kjører én spørring per fylke. Målt krysningspunkt: 3 fylker.
    ///
    /// Uten denne grensen gikk `reise:kartlegging` fra 525 til 1702 ms og
    /// `reise:alt-paa-en-gang` fra 1207 til 4350 ms i Full-kjøringen — en
    /// regresjon innført av en optimalisering.
    /// </summary>
    [Fact]
    public void Flere_enn_to_fylker_gir_ingen_plan()
    {
        var utdata = new[] { Omr(Fylke, "50"), Omr(Fylke, "46"), Omr(Fylke, "31") };

        Plan(utdata, ["5001", "4601", "3101"]).Should().BeNull();
    }

    /// <summary>Nøyaktig på taket skal fortsatt gi plan.</summary>
    [Fact]
    public void To_fylker_er_innenfor_taket()
    {
        Plan([Omr(Fylke, "50"), Omr(Fylke, "46")], ["5001", "4601"])
            .Should().NotBeNull();
    }
}
