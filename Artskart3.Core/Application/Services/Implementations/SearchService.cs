using Artskart3.Core.Application.Converters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using System.Collections.Generic;

namespace Artskart3.Core.Application.Services.Implementations
{
    public class SearchService : ISearchService
    {
        private readonly ISearchRepository _searchRepository;

        public SearchService(ISearchRepository searchRepository)
        {
            _searchRepository = searchRepository;
        }

        public async Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                filter = filter ?? new LocationSearchFilterDto();
                
                var locations = _searchRepository.GetLocationsAsync(filter, cancellationToken);
                return await GeoJsonConverter.LocationsToGeoJson(locations, StyleType.Unknown, filter.Epsg, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException("An error occurred while processing your location search request.", ex);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while processing your location search request.", ex);
            }
        }


        public async Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _searchRepository.GetObservationsAsync(filter, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException("An error occurred while processing observation search request.", ex);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while processing observation search request.", ex);
            }
        }


        public async Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default)
        {
           var alltaxons = await _searchRepository.GetTaxonsAsync(name, maxCount, cancellationToken);           
           return alltaxons;
        }

        public async Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(int[] areaTypeIds, CancellationToken cancellationToken = default)
        {
            var areas = await _searchRepository.GetAreasByTypeIdsAsync(areaTypeIds, cancellationToken);
            return areas;
        }
    }
}