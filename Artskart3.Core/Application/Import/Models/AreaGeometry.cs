using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Ett område med geometrien sin, slik geografitjenesten trenger det. Flat kopi av en
/// <c>Area</c>-rad — ingen navigasjonsegenskaper, fordi dette lastes én gang og holdes
/// i minnet.
/// </summary>
/// <param name="Id">Primærnøkkelen. Det er denne <c>LocationAreas</c> peker på.</param>
/// <param name="Fid">Områdets kode i kilden. Sorteringsnøkkel innenfor en områdetype.</param>
/// <param name="Name">Områdenavnet. Nøkkelen kommunesentroidene slås opp på.</param>
/// <param name="AreaTypeId">1 kommune, 2 fylke, 3 verneområde, 4 havområde, 5 marine områder, 6 Svalbard.</param>
/// <param name="IsRequired">
/// Om områdetypen er påkrevd. Treffer en koordinat ingen påkrevd type, regnes den som
/// utenfor Norge og posten avvises — se <c>IGeographyService.GetAreasForCoordinate</c>.
/// </param>
/// <param name="IsCurrent">
/// Om området gjelder i dag. Historiske områder er med i oppslaget: en observasjon fra
/// 1950 hører til kommunen som fantes da, og <c>LocationAreas</c> i Artskart 2
/// inneholder begge. Det er den avledede indeksen som filtrerer på gjeldende.
/// </param>
/// <param name="ParentFid">Fylkets <c>Fid</c> for en kommune. Brukes til kommunesentroidene.</param>
/// <param name="Geometry">Omrisset, merket 25833 etter <c>RelabelGeometrySridTo25833</c>.</param>
public sealed record AreaGeometry(
    int Id,
    string Fid,
    string? Name,
    int AreaTypeId,
    bool IsRequired,
    bool IsCurrent,
    string? ParentFid,
    Geometry Geometry);
