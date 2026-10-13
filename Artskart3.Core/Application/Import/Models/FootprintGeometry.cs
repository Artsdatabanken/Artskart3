using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Resultatet av å tolke <c>footprintWKT</c>.
/// </summary>
/// <param name="Geometry">Omrisset i ETRS89 / UTM 33N, merket 25833.</param>
/// <param name="Wkt">
/// Samme omriss som tekst, avrundet. Artskart 2 lagrer denne strengen på
/// <c>Observation.Geometry</c> (<c>[NotMapped]</c>) fram til lokaliteten bygges.
/// </param>
/// <param name="PrecisionInMeters">
/// Halve diagonalen i det omsluttende rektangelet. For et funksjonsområde på noen
/// kvadratkilometer blir det hundrevis av meter — som er riktig: en observasjon knyttet
/// til et område er ikke stedfestet mer presist enn området.
/// </param>
/// <param name="CentroidLatLon">
/// Senterpunktet i lengde- og breddegrad, satt bare når omrisset kom i grader og
/// posten manglet koordinat. <c>null</c> ellers.
/// </param>
public sealed record FootprintGeometry(
    Geometry Geometry,
    string Wkt,
    int PrecisionInMeters,
    GeoPoint? CentroidLatLon);

/// <summary>
/// Senterpunktet i en kommune eller et fylke.
/// </summary>
/// <param name="Point">Senterpunktet, merket 25833.</param>
/// <param name="PrecisionInMeters">Halve diagonalen i områdets omsluttende rektangel.</param>
/// <param name="MatchedOn">
/// Hvilket ledd i oppslagskjeden som traff. Tas med fordi kjeden er slepphendt nok at
/// det er verdt å kunne se hvorfor en post fikk senterpunktet sitt.
/// </param>
public sealed record CentroidHit(GeoPoint Point, int PrecisionInMeters, string MatchedOn);
