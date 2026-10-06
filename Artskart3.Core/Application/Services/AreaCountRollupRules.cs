using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Avgjør når områdetellingen kan ankre på kommuneradene og rulle opp til
/// fylke, i stedet for å ankre på fylkesraden og filtrere ned med en semijoin.
///
/// Dette er det motsatte problemet av <see cref="AreaCountGeoJoinRules"/>. Der
/// handler det om når semijoinen er overflødig; her om formen der den IKKE er
/// det — valget er finere enn utdatanivået — og hvor den kan unngås ved å snu
/// spørringen.
///
/// Forutsetningen er bare <c>Area.ParentFid</c>, som vi uansett stoler på: det
/// er den samme koblingen <c>FilterAreasBySelection</c> bruker for å bestemme
/// hvilke fylkesceller et kommunevalg i det hele tatt skal vise.
///
/// HVA SOM IKKE LAR SEG RULLE OPP
/// Verneområder. De har <c>ZoomLevel 0</c> og blir aldri utdataceller, og de
/// nøster ikke i fylker — et naturreservat kan krysse både kommune- og
/// fylkesgrenser. Det finnes ingen forelder å summere til. Havområder er
/// utdataceller i seg selv og kan ikke utledes av kommunerader.
/// </summary>
public static class AreaCountRollupRules
{
    /// <summary>
    /// Høyeste antall utdatafylker opprullingen tas for.
    ///
    /// Opprullingen kjører ÉN SPØRRING PER FYLKE, så kostnaden ganges opp med
    /// antall fylker, mens semijoin-formen er én spørring uansett. Målt med
    /// koordpresisjon- og periodefilter over de 15 kommunene i
    /// <c>kommune:mange</c> (9 fylker), samme svar i alle:
    ///
    /// <code>
    ///    1 fylke     439 ms opprulling  mot   728 ms semijoin
    ///    2 fylker    547 ms             mot  1946 ms
    ///    3 fylker    812 ms             mot   658 ms   &lt;- krysningspunkt
    ///    5 fylker   1315 ms             mot   877 ms
    ///   15 fylker   2354 ms             mot   878 ms
    /// </code>
    ///
    /// Taket står der målingen sier, ikke på en rund antakelse. Uten det gikk
    /// <c>reise:kartlegging</c> fra 525 til 1702 ms og
    /// <c>reise:alt-paa-en-gang</c> fra 1207 til 4350 ms i Full.
    ///
    /// Skal taket heves, må opprullingen først bli én spørring — gruppert på et
    /// CASE-uttrykk som mapper kommune til fylke, med
    /// <c>COUNT(DISTINCT ObservationId)</c>. Da forsvinner multiplikatoren, og
    /// formen ville vært best i alle tilfellene over. Ikke forsøkt.
    /// </summary>
    private const int MaxFylker = 2;

    /// <summary>
    /// Fylkets entitets-id → kommunene i det som er valgt. Null når formen ikke
    /// gjelder, og da skal den vanlige tellingen brukes.
    ///
    /// <paramref name="kommuneTilFylkeFid"/> er
    /// <c>IAreaHierarchyService.GetCountyFid</c>.
    /// </summary>
    public static Dictionary<int, int[]>? LagOpprullingsplan(
        IReadOnlyList<Area> utdataomraader,
        string[]? kommuneFids,
        bool harFylkesvalg,
        bool harHavomraadevalg,
        bool harVerneomraadevalg,
        Func<string, int?> fidTilEntityId,
        Func<string, string?> kommuneTilFylkeFid)
    {
        if (kommuneFids is null || kommuneFids.Length == 0) return null;

        // Alle tre gjør utdatacellene til noe annet enn «fylkene kommunene
        // ligger i», og da kan de ikke utledes av kommunerader alene.
        //
        // Fylkesvalg: geo-predikatet er en ELLER, så et fylkesvalg slipper
        // gjennom observasjoner utenfor de valgte kommunene. De ville falt bort.
        if (harFylkesvalg || harHavomraadevalg || harVerneomraadevalg) return null;

        if (utdataomraader.Count == 0) return null;

        // Utdata må være rene fylkesceller. Er Svalbard eller noe annet med,
        // faller vi tilbake framfor å gjette hvordan det skal rulles opp.
        if (utdataomraader.Any(a => a.AreaTypeId != (int)ObservationIndexEntityType.County))
            return null;

        var plan = new Dictionary<int, List<int>>();

        foreach (var kommuneFid in kommuneFids)
        {
            var kommuneId = fidTilEntityId(kommuneFid);
            if (kommuneId is null) return null;

            var fylkeFid = kommuneTilFylkeFid(kommuneFid);
            if (fylkeFid is null) return null;

            var fylkeId = fidTilEntityId(fylkeFid);
            if (fylkeId is null) return null;

            if (!plan.TryGetValue(fylkeId.Value, out var liste))
            {
                liste = [];
                plan[fylkeId.Value] = liste;
            }
            liste.Add(kommuneId.Value);
        }

        // Settene må stemme nøyaktig. Mangler et utdatafylke i planen, ville
        // cellen blitt stående tom i stedet for å få sitt tall; ligger et fylke
        // i planen uten å være utdata, har vi misforstått hvordan lista ble
        // snevret. Begge deler er grunn til å la være.
        var utdataFylker = new HashSet<int>();
        foreach (var omraade in utdataomraader)
        {
            var id = fidTilEntityId(omraade.Fid);
            if (id is null) return null;
            utdataFylker.Add(id.Value);
        }

        if (!utdataFylker.SetEquals(plan.Keys)) return null;

        // Én spørring per fylke. Over taket taper formen mot semijoinen.
        if (plan.Count > MaxFylker) return null;

        return plan.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
