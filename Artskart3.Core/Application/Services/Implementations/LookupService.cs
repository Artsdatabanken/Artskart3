using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations
{
    public class LookupService : ILookupService
    {
        private readonly ILookupRepository _lookupRepository;

        public LookupService(ILookupRepository lookupRepository)
        {
            _lookupRepository = lookupRepository ?? throw new ArgumentNullException(nameof(lookupRepository));
        }

        public Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync()
        {
            return _lookupRepository.GetCategoriesAsync();
        }

        public Task<IEnumerable<AreaTypeDto>> GetAreasAsync()
        {
            return _lookupRepository.GetAreasAsync();
        }

        public Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync()
        {
            return _lookupRepository.GetInstitutionsAsync();
        }

        public Task<IEnumerable<TaxonGroupDto>> GetTaxonGroupsAsync()
        {
            return _lookupRepository.GetTaxonGroupsAsync();
        }

        public Task<IEnumerable<BehaviorDto>> GetBehaviorsAsync()
        {
            return _lookupRepository.GetBehaviorsAsync();
        }

        public Task<IEnumerable<BasisOfRecordDto>> GetBasisOfRecordsAsync()
        {
            return _lookupRepository.GetBasisOfRecordsAsync();
        }
    }
}
