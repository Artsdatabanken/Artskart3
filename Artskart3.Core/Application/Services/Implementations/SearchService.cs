using Artskart3.Core.Application.Converters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
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

        public async Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null)
        {
            try
            {
                filter = filter ?? new LocationSearchFilterDto();
                
                var locations = _searchRepository.GetLocationsAsync(filter);
                return await GeoJsonConverter.LocationsToGeoJson(locations, StyleType.Unknown, filter.Epsg);
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

        public async Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20)
        {
           var alltaxons = await _searchRepository.GetTaxonsAsync(name, maxCount);
           return alltaxons;
        }

        public async Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(params int[] areaTypeIds)
        {
            var areas = await _searchRepository.GetAreasByTypeIdsAsync(areaTypeIds);
            return areas;
        }
    }
}