using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Avgjør når geo-semijoinen i områdetellingen kan utelates.
///
/// BAKGRUNNEN
/// <c>ComputeFilteredAreaCounts</c> bygger spørringen i to deler. Ankerleddet
/// velger radene som blir til utdataceller — <c>EntityTypeId IN (…) AND EntityId
/// IN (…)</c>. Deretter legges det på et <c>EXISTS</c> mot samme tabell som
/// sjekker at observasjonen ligger i et av de valgte områdene.
///
/// Ved zoomnivå 1 med fylkesfilter er ankerraden ALLEREDE fylkesraden, og
/// semijoinen spør om nettopp den raden. Den fjerner da ingenting, men koster en
/// selvjoin over hele ankersettet. Målt på prodlik base, fylke Trøndelag med tre
/// attributtfiltre: 1634 ms med leddet, 169 ms uten — identisk svar.
///
/// TO GRUNNER, HOLDT ADSKILT
/// Metoden returnerer sant av to ulike årsaker, og forskjellen er viktig nok til
/// at de beregnes hver for seg:
///
/// 1. DELMENGDEREGELEN er et bevis. Er utdataområdet selv et valgt område,
///    tilfredsstiller ankerraden <c>EXISTS</c>-leddet ved seg selv — spørringen
///    sier <c>A ∧ ∃x(… A …)</c> med vitnet i raden vi står på. Det følger av
///    spørringens form og holder på et hvilket som helst datasett.
///
/// 2. ETTERKOMMERREGELEN er en forutsetning. Er utdataområdet en kommune i et
///    valgt fylke, tilfredsstiller ankerraden IKKE leddet — en kommunerad er
///    ingen fylkesrad. Det er en ANNEN rad som gjør det, og at den alltid finnes
///    er en egenskap ved hvordan indeksen bygges.
///
///    Invarianten håndheves i <c>Scripts/BackfillAll.sql</c>, som avbryter
///    dersom det finnes en kommunerad uten tilhørende fylkesrad. Målt til 0
///    brudd over 357 kommuner. Holder den ikke, blir tallene på kartet for høye
///    uten noen feilmelding — derfor stopper backfillen i stedet for å advare.
///
/// Skal etterkommerregelen rulles tilbake en dag, er det én linje: returner
/// <c>alleErValgte</c> alene. Ikke slå de to uttrykkene sammen — da forsvinner
/// skillet mellom hva som er bevist og hva som er forutsatt.
/// </summary>
public static class AreaCountGeoJoinRules
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;

    /// <summary>
    /// Sant når <c>EXISTS</c>-leddet ikke kan fjerne noen rad, og dermed kan utelates.
    ///
    /// <paramref name="fidTilEntityId"/> er <c>IAreaHierarchyService.FidToEntityId</c>.
    /// Id-listene er de samme som bygger geo-predikatet, etter at fylkesvalget er
    /// delt mellom fylke og Svalbard.
    /// </summary>
    public static bool CanSkipGeoJoin(
        IReadOnlyList<Area> utdataomraader,
        Func<string, int?> fidTilEntityId,
        int[] kommuneIder,
        int[] fylkeIder,
        int[] svalbardIder,
        int[] verneomraadeIder,
        int[] havomraadeIder)
    {
        // Ingen utdataceller: CountResolver leser ingenting uansett.
        if (utdataomraader.Count == 0) return true;

        var valgte = new HashSet<(int Type, int Id)>();
        foreach (var id in kommuneIder) valgte.Add((Kommune, id));
        foreach (var id in fylkeIder) valgte.Add(((int)ObservationIndexEntityType.County, id));
        foreach (var id in svalbardIder) valgte.Add(((int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen, id));
        foreach (var id in verneomraadeIder) valgte.Add(((int)ObservationIndexEntityType.RestrictedArea, id));
        foreach (var id in havomraadeIder) valgte.Add(((int)ObservationIndexEntityType.OceanArea, id));

        // Filteret oppga områder, men ingen av dem lot seg løse opp til en id.
        // Da slipper geo-predikatet ingenting gjennom, og å utelate det ville
        // gjort et tomt svar til et ufiltrert et. Behold leddet.
        if (valgte.Count == 0) return false;

        var fylkesvalg = new HashSet<int>(fylkeIder);

        var alleErValgte = true;
        var alleErValgteEllerEtterkommere = true;

        foreach (var omraade in utdataomraader)
        {
            var id = fidTilEntityId(omraade.Fid);

            // Lar ikke Fid-en seg tolke, vet vi ikke hvilket par cellen er, og
            // kan ikke slutte noe. «Vet ikke» skal beholde leddet.
            if (id is null) return false;

            if (valgte.Contains((omraade.AreaTypeId, id.Value))) continue;

            alleErValgte = false;

            var erKommuneIValgtFylke =
                omraade.AreaTypeId == Kommune
                && omraade.ParentFid != null
                && fidTilEntityId(omraade.ParentFid) is int fylkeId
                && fylkesvalg.Contains(fylkeId);

            if (!erKommuneIValgtFylke)
            {
                alleErValgteEllerEtterkommere = false;
                break;
            }
        }

        // Den første impliserer den andre. Begge står likevel navngitt, slik at
        // tilbakefallet til bare det beviste er én linje. Se klassekommentaren.
        return alleErValgte || alleErValgteEllerEtterkommere;
    }
}
