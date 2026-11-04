using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vokter når geo-semijoinen i områdetellingen kan utelates.
///
/// Begge retninger er farlige, men ikke like farlige. Hopper vi over leddet når
/// det faktisk filtrerer, blir tallene på kartet FOR HØYE — stille, uten
/// feilmelding. Beholder vi det når det er overflødig, blir kallet bare tregt.
/// Testene nedenfor legger derfor mest vekt på tilfellene der svaret skal være
/// «behold».
/// </summary>
public class AreaCountGeoJoinRulesTests
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;
    private const int Havomraade = (int)ObservationIndexEntityType.OceanArea;
    private const int Svalbard = (int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen;

    /// <summary>Samme regel som AreaHierarchyService.FidToEntityId.</summary>
    private static int? FidTilId(string fid) =>
        int.TryParse(fid.Replace("_", ""), out var id) ? id : null;

    private static Area Omr(int type, string fid, string? parentFid = null)
    {
        var omraade = new Area { AreaTypeId = type, Fid = fid };
        if (parentFid != null) omraade.ParentFid = parentFid;
        return omraade;
    }

    private static bool Kan(
        IReadOnlyList<Area> utdata,
        int[]? kommune = null, int[]? fylke = null, int[]? svalbard = null,
        int[]? verne = null, int[]? hav = null)
        => AreaCountGeoJoinRules.CanSkipGeoJoin(
            utdata, FidTilId, kommune ?? [], fylke ?? [], svalbard ?? [], verne ?? [], hav ?? []);

    // --- DELMENGDEREGELEN: beviselig trygt -----------------------------------

    /// <summary>
    /// Zoomnivå 1 med fylkesfilter. Utdatacellen ER fylket, så ankerraden
    /// tilfredsstiller predikatet ved seg selv. Det tregeste tilfellet i suiten:
    /// 1634 ms med leddet, 169 ms uten.
    /// </summary>
    [Fact]
    public void Fylke_som_utdata_og_som_valg_kan_hoppe_over()
    {
        Kan([Omr(Fylke, "50")], fylke: [50]).Should().BeTrue();
    }

    /// <summary>
    /// Fylke og havområde samtidig. Begge utdatacellene er valgte områder, så
    /// hver rad tilfredsstiller sin egen gren av ELLER-et.
    /// </summary>
    [Fact]
    public void Fylke_og_havomraade_kan_hoppe_over()
    {
        Kan([Omr(Fylke, "50"), Omr(Havomraade, "13")], fylke: [50], hav: [13])
            .Should().BeTrue();
    }

    /// <summary>
    /// Et fylkesvalg slås opp mot både fylke og Svalbard. Er utdatacellen
    /// Svalbard-området, er den fortsatt et valgt par.
    /// </summary>
    [Fact]
    public void Svalbard_som_utdata_kan_hoppe_over()
    {
        Kan([Omr(Svalbard, "2101")], fylke: [], svalbard: [2101]).Should().BeTrue();
    }

    /// <summary>Zoomnivå 2 med kommunefilter: utdata er nøyaktig de valgte.</summary>
    [Fact]
    public void Kommuner_som_utdata_og_som_valg_kan_hoppe_over()
    {
        Kan([Omr(Kommune, "5001", "50"), Omr(Kommune, "5006", "50")], kommune: [5001, 5006])
            .Should().BeTrue();
    }

    // --- ETTERKOMMERREGELEN: hviler på invarianten ---------------------------

    /// <summary>
    /// Zoomnivå 2 med fylkesfilter. Her er ankerraden en KOMMUNErad, og det er
    /// fylkesraden som tilfredsstiller predikatet. At den alltid finnes
    /// håndheves av invariantsjekken i BackfillAll.sql.
    /// </summary>
    [Fact]
    public void Kommuner_i_valgt_fylke_kan_hoppe_over()
    {
        Kan([Omr(Kommune, "5001", "50"), Omr(Kommune, "5006", "50")], fylke: [50])
            .Should().BeTrue();
    }

    /// <summary>
    /// Blandet: én kommune valgt direkte, resten arvet fra fylkesvalget. Begge
    /// reglene må gjelde per celle, ikke for hele settet under ett.
    /// </summary>
    [Fact]
    public void Blandet_direkte_valgt_og_arvet_kan_hoppe_over()
    {
        Kan([Omr(Kommune, "5001", "50"), Omr(Kommune, "4601", "46")],
            kommune: [4601], fylke: [50])
            .Should().BeTrue();
    }

    // --- MÅ BEHOLDES ---------------------------------------------------------

    /// <summary>
    /// DEN VIKTIGSTE. Zoomnivå 1 med kommunefilter: utdata er fylker, valget er
    /// kommuner. Her fjerner leddet faktisk rader — observasjoner i fylket, men
    /// utenfor den valgte kommunen. Hoppet vi over, ville fylket fått hele sitt
    /// eget antall i stedet for kommunens.
    /// </summary>
    [Fact]
    public void Fylke_som_utdata_med_kommunevalg_maa_beholde()
    {
        Kan([Omr(Fylke, "50")], kommune: [5001]).Should().BeFalse();
    }

    /// <summary>
    /// Verneområder har ZoomLevel 0 og blir aldri utdataceller. Et verneområde­valg
    /// snevrer derfor ikke områdelista, og leddet er det eneste som filtrerer.
    /// </summary>
    [Fact]
    public void Verneomraadevalg_maa_beholde()
    {
        Kan([Omr(Fylke, "50"), Omr(Fylke, "46")], verne: [12345]).Should().BeFalse();
    }

    /// <summary>
    /// En kommune hvis forelderfylke IKKE er valgt. Kan oppstå om områdelista
    /// snevres på et annet grunnlag enn fylkesvalget.
    /// </summary>
    [Fact]
    public void Kommune_utenfor_valgt_fylke_maa_beholde()
    {
        Kan([Omr(Kommune, "5001", "50"), Omr(Kommune, "4601", "46")], fylke: [50])
            .Should().BeFalse();
    }

    /// <summary>Kommune uten forelder kan ikke knyttes til noe fylke.</summary>
    [Fact]
    public void Kommune_uten_forelder_maa_beholde()
    {
        Kan([Omr(Kommune, "5001")], fylke: [50]).Should().BeFalse();
    }

    /// <summary>
    /// Filteret oppga områder, men ingen lot seg løse opp til en id. Da slipper
    /// predikatet ingenting gjennom, og å utelate det ville gjort et tomt svar
    /// til et ufiltrert et — den verste feilen i hele settet.
    /// </summary>
    [Fact]
    public void Ingen_oppslagbare_ider_maa_beholde()
    {
        Kan([Omr(Fylke, "50")]).Should().BeFalse();
    }

    /// <summary>
    /// Lar ikke Fid-en seg tolke, vet vi ikke hvilket par cellen er. «Vet ikke»
    /// skal beholde leddet, ikke anta det beste.
    /// </summary>
    [Fact]
    public void Utolkbar_fid_maa_beholde()
    {
        Kan([Omr(Fylke, "Naturbase VV50")], fylke: [50]).Should().BeFalse();
    }

    /// <summary>
    /// Én celle som ikke kvalifiserer skal diskvalifisere hele spørringen —
    /// leddet kan ikke utelates for bare noen av radene.
    /// </summary>
    [Fact]
    public void En_ukvalifisert_celle_diskvalifiserer_hele()
    {
        Kan([Omr(Fylke, "50"), Omr(Fylke, "46")], fylke: [50]).Should().BeFalse();
    }

    /// <summary>Ingen utdataceller: ingenting leses uansett.</summary>
    [Fact]
    public void Tom_omraadeliste_kan_hoppe_over()
    {
        Kan([], fylke: [50]).Should().BeTrue();
    }
}
