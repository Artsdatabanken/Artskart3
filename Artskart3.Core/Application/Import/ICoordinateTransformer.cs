using Artskart3.Core.Application.Import.Models;
using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Transformerer koordinater mellom de koordinatsystemene importen møter.
/// Implementasjonen eier projeksjonsbiblioteket.
/// </summary>
public interface ICoordinateTransformer
{
    /// <summary>
    /// Punktet uttrykt i <paramref name="targetEpsg"/>. Samme kode inn og ut gir
    /// punktet tilbake urørt.
    ///
    /// Resultatet rundes ikke. Det er <c>ImportValueHelper.SetDigitsForLatLon</c> som
    /// bestemmer antall desimaler på lagrede lengde- og breddegrader.
    /// </summary>
    GeoPoint Transform(GeoPoint point, int targetEpsg);

    /// <summary>
    /// Geometrien uttrykt i <paramref name="targetEpsg"/>, med koordinatene rundet til
    /// <paramref name="decimals"/> desimaler slik Artskart 2 gjør det (åtte som
    /// standard, «banker's rounding»). Den opprinnelige geometrien er urørt, og
    /// resultatet er merket med målsystemet.
    /// </summary>
    Geometry TransformGeometry(Geometry geometry, int sourceEpsg, int targetEpsg, int decimals = 8);
}
