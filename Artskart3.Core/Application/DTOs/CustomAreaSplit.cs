namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Et egendefinert område delt i områdene som ligger helt innenfor det, og lokasjonene
/// langs kanten.
///
/// Et område som ligger helt innenfor polygonet, har bare lokasjoner som snitter polygonet,
/// siden LocationAreas er bygget med samme STIntersects-regel mot Area.WktPolygon. Søket kan
/// derfor bruke områdene som et vanlig områdefilter og sende bare kantlokasjonene som liste,
/// i stedet for alle polygonets lokasjoner.
/// </summary>
/// <param name="InsideAreas">
/// Gjeldende områder helt innenfor polygonet, som (AreaTypeId, Fid). Et fylke helt innenfor
/// erstatter kommunene sine.
/// </param>
/// <param name="EdgeLocationIds">
/// Polygonets lokasjoner uten rad i noen av områdene, eller null når de er flere enn
/// <see cref="Constants.SearchConstants.MaxCustomAreaLocations"/>.
/// </param>
public sealed record CustomAreaSplit(IReadOnlyList<(int AreaTypeId, string Fid)> InsideAreas, int[]? EdgeLocationIds);
