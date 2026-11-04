using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Hele polygondatasettet i minnet, med spørringene kjørt mot det i stedet for
/// mot databasen.
///
/// HVORFOR DET I DET HELE TATT ER MULIG
/// Polygon- og linjelokasjoner er 1,5 % av lokasjonene — 73 908 av 5 100 794 —
/// og bærer 3 946 488 observasjoner. Det er lite nok til å ligge i en
/// API-prosess, og det er hele grunnlaget: for punktlokasjonene ville det
/// samme vært 61 millioner observasjoner.
///
/// HVA DET LØSER SOM EN BUFFER IKKE KAN
/// Bufferne lagrer marginaler og svarer bare på null eller ett filter; to
/// filtre krever samfordelingen. En skanning over observasjonene har ikke det
/// problemet — den evaluerer predikatene direkte, så fire filtre koster det
/// samme som ett.
///
/// Målt før dette: 152 av 266 sekunder på LocationPolygons i Full-kjøringen
/// gikk til filterkombinasjoner bufferen ikke kunne besvare.
/// </summary>
public interface IPolygonLocationStore
{
    /// <summary>
    /// Om datasettet er ferdig bygget. Før det svarer <see cref="TryQuery"/>
    /// alltid null, og kalleren spør databasen som før.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Kjører hele polygonspørringen mot minnet, eller returnerer <c>null</c>
    /// dersom datasettet ikke er bygget ennå.
    ///
    /// Resultatet skal være identisk med det databasestien ville gitt —
    /// samme lokasjoner, samme antall, samme rekkefølge, samme
    /// rektangel- og boksfiltrering. Er det ikke det, er lageret feil.
    /// </summary>
    IReadOnlyList<LocationPolygonDto>? TryQuery(LocationSearchFilterDto filter, int maxResults);

    /// <summary>Bygger datasettet. Kalles én gang ved oppstart.</summary>
    Task BuildAsync(CancellationToken cancellationToken = default);

    /// <summary>Tall for logging og feilsøking.</summary>
    string Describe();
}
