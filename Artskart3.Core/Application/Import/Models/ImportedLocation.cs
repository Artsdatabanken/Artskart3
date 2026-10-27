using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// En lokalitet under bygging. Lokaliteter deles på tvers av observasjoner, så den
/// slås opp eller opprettes på <see cref="LookupId"/> (fase 6).
/// </summary>
public class ImportedLocation
{
    /// <summary>
    /// Nøkkelen lokaliteter deles på. Artskart 2 bygger den i
    /// <c>Location.GenerateId</c>: for node 1010 av <c>locationID</c>, ellers av
    /// <c>{node}_{east}_{north}_{precision}</c>. Den er grunnen til at node 1010 får
    /// egne lokalitetsrader for samme sted (plan 4 i bugdokumentet).
    /// </summary>
    public string? LookupId { get; set; }

    /// <summary>Kildens <c>locationID</c>, trunkert til 50 tegn.</summary>
    public string? LocationId { get; set; }

    public int NodeId { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public int? CoordinatePrecision { get; set; }

    /// <summary>Kolonnen tar 1000 tegn, mens <c>ObservationDetail.Locality</c> tar 100.</summary>
    public string? Locality { get; set; }

    /// <summary>
    /// Geometrien fra <c>footprintWKT</c>, eller punktet. Merkes 25833 — se
    /// migrasjonen <c>RelabelGeometrySridTo25833</c>.
    /// </summary>
    public Geometry? Geometry { get; set; }

    /// <summary>
    /// Områdene lokaliteten ligger i, fra <c>GeographyService.GetAreasForCoordinate</c>.
    /// Ingen treff betyr at posten avvises med <c>NoHitOnRequiredAreadatasets</c>.
    ///
    /// Navnet er med fordi Jan Mayen-regelen i <c>ImportGeometryStep</c> slår opp på
    /// det. Geometrien er ikke med — den ville ligget på hver enkelt lokalitet i en
    /// batch på 250 000.
    /// </summary>
    public List<LocationArea> Areas { get; } = [];
}

/// <summary>
/// Et område en lokalitet ligger i, slik det følger med inn i <c>LocationAreas</c>.
/// </summary>
/// <param name="AreaId">Primærnøkkelen <c>LocationAreas</c> peker på.</param>
/// <param name="AreaTypeId">1 kommune, 2 fylke, 3 verneområde, 4 havområde, 5 marine områder, 6 Svalbard.</param>
/// <param name="Fid">Områdets kode i kilden.</param>
/// <param name="Name">Områdenavnet.</param>
public sealed record LocationArea(int AreaId, int AreaTypeId, string Fid, string? Name);
