using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Geografioppslagene importen gjør per post. Port av Artskart 2s
/// <c>IGeographyService</c>, begrenset til de fire metodene importstien bruker —
/// resten av grensesnittet der er lasteverktøy for områdedata og hører ikke hit.
/// </summary>
public interface IGeographyService
{
    /// <summary>
    /// Områdene en koordinat ligger i, i ETRS89 / UTM 33N.
    ///
    /// TOM LISTE BETYR «UTENFOR NORGE», IKKE «INGEN OMRÅDER». Treffer koordinaten
    /// områder, men ingen av dem har en påkrevd områdetype (kommune, fylke eller
    /// Svalbard), returneres en tom liste — og da avvises posten med
    /// <c>NoHitOnRequiredAreadatasets</c>. Et treff på bare et verneområde er altså
    /// ikke nok. Slik er regelen i Artskart 2, og den er det som holder utenlandske
    /// koordinater ute.
    ///
    /// Rekkefølgen er områdetype, så <c>Fid</c>.
    /// </summary>
    IReadOnlyList<AreaGeometry> GetAreasForCoordinate(int east, int north);

    /// <summary>
    /// Om koordinaten ligger innenfor et grovt polygon rundt Norge med omliggende
    /// havområder. Billig forhåndssjekk før det dyrere områdeoppslaget.
    /// </summary>
    bool CloseToNorway(int east, int north);

    /// <summary>
    /// Tolker <c>footprintWKT</c> fra kilden: validerer, transformerer fra lengde- og
    /// breddegrad hvis det er det den er oppgitt i, runder av og regner ut presisjonen
    /// omrisset gir.
    /// </summary>
    /// <param name="footprintWkt">Verdien fra kilden.</param>
    /// <param name="footprintSrs">
    /// <c>UnknownFields["footprintSRS"]</c> hvis kilden oppgav den. <c>EPSG:4326</c>
    /// betyr lengde- og breddegrad.
    /// </param>
    /// <param name="decimals">
    /// Antall desimaler resultatet rundes til, eller -1 for å la koordinatene stå.
    /// </param>
    /// <param name="result">Omrisset og presisjonen, eller <c>null</c>.</param>
    bool TryValidateFootprint(
        string? footprintWkt,
        string? footprintSrs,
        int decimals,
        out FootprintGeometry? result);

    /// <summary>
    /// Senterpunktet i en kommune eller et fylke, oppgitt ved navn. Siste utvei for en
    /// post uten koordinat.
    ///
    /// Oppslaget er bevisst slepphendt — se implementasjonen for kjeden av fallbacks.
    /// Treffer det, settes koordinaten til senterpunktet og presisjonen til halve
    /// diagonalen i områdets omsluttende rektangel, altså titalls kilometer.
    /// </summary>
    bool TryGetMunicipalityCentroid(string? municipality, string? county, out CentroidHit? result);
}
