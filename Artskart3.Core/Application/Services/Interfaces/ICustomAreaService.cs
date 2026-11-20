namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Oppslag av lokasjonene i et egendefinert område tegnet i kartet.
/// </summary>
public interface ICustomAreaService
{
    /// <summary>
    /// Lokasjonene som snitter polygonet, eller null når de er flere enn
    /// <see cref="Constants.SearchConstants.MaxCustomAreaLocations"/>. <paramref name="wkt"/>
    /// er <see cref="DTOs.IObservationFilter.CustomAreaWkt"/>, allerede validert.
    /// </summary>
    Task<int[]?> GetLocationIdsAsync(string wkt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polygonet delt i områdene helt innenfor det og lokasjonene langs kanten, eller null
    /// når ingen områder ligger helt innenfor.
    /// </summary>
    Task<DTOs.CustomAreaSplit?> GetSplitAsync(string wkt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lokasjonene i <paramref name="locationIds"/> uten rad i noen kommune, noe havområde eller
    /// Svalbard. Funnene der har ingen rader av de typene i ObservationEntityIndex, så
    /// listevisningen henter dem fra Observation.
    /// </summary>
    Task<int[]> GetLocationsWithoutAreasAsync(int[] locationIds, CancellationToken cancellationToken = default);
}
