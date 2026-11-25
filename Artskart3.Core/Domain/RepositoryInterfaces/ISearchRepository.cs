using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface ISearchRepository
{
    Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default);
    Task<List<LocationModel>> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<LocationCountDto> GetLocationCountAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<ObservationImageDto>> GetObservationImagesAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default);
    Task<IEnumerable<ObservationListInfoDto>> GetObservationListInfo(ObservationListInfoRequestDto request, CancellationToken cancellationToken = default);
    Task<IEnumerable<AreaMarkerDto>> GetAreaMarkersAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<LocationPolygonDto>> GetLocationPolygonsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<AreaCountDto>> GetAreaCountsAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fasett-tall for én dimensjon, aggregert live — antall per bøtte, med filteret
    /// anvendt minus dimensjonens egen gruppe. Fall-through når fasett-bufferen ikke
    /// dekker filteret.
    /// </summary>
    /// <param name="filter">Filteret slik det kom inn.</param>
    /// <param name="facet">Dimensjonen det skal telles per bøtte for.</param>
    /// <param name="mask">Bool-filtrene som skal gjelde, se <see cref="FilterMask"/>.</param>
    /// <param name="cadastralLocationIds">
    /// Matrikkelenhetene allerede løst opp til lokasjons-id-er, eller null når filteret
    /// ikke har noen. Løses opp av kalleren, ikke her: oppslaget går mot Kartverket, og
    /// fasett-tallene kjører flere aggregeringer på samme filter — de skal dele ett
    /// oppslag, ikke gjøre ett hver.
    /// </param>
    /// <param name="cancellationToken">Avbrytelse.</param>
    Task<Dictionary<int, int>> AggregateFacetCountsAsync(
        IObservationFilter filter, FacetDimension facet, FilterMask.Selection mask,
        int[]? cadastralLocationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Antall observasjoner for filteret med ett bool-alternativ satt. Fall-through for
    /// maskefasettene.
    /// </summary>
    Task<int> AggregateFacetTotalAsync(
        IObservationFilter filter, FilterMask.Selection mask,
        int[]? cadastralLocationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Matrikkelenhetene i filteret løst opp til lokasjons-id-er, eller null når det
    /// ikke er noen. Offentlig fordi fasett-tellingene må gjøre oppslaget ÉN gang og
    /// gjenbruke svaret på tvers av aggregeringene.
    /// </summary>
    Task<int[]?> ResolveCadastralLocationIdsAsync(
        IObservationFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fasett-tall for områdelistene: antall per område-Fid, med filteret anvendt minus
    /// brukerens egen geografi og minus kartutsnittet.
    ///
    /// Dekker fylker, kommuner, havområder og Svalbard i én omgang. Områder uten treff
    /// utelates — kalleren viser 0 for det som mangler.
    /// </summary>
    Task<Dictionary<string, int>> GetAreaFacetCountsAsync(
        IObservationFilter filter, CancellationToken cancellationToken = default);
}
