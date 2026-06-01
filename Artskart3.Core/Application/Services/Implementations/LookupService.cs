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

        public Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetCategoriesAsync(cancellationToken);
        }

        public Task<IEnumerable<AreaTypeDto>> GetAreasAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetAreasAsync(cancellationToken);
        }

        public Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetInstitutionsAsync(cancellationToken);
        }

        public Task<IEnumerable<TaxonGroupDto>> GetTaxonGroupsAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetTaxonGroupsAsync(cancellationToken);
        }

        public Task<IEnumerable<BehaviorDto>> GetBehaviorsAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetBehaviorsAsync(cancellationToken);
        }

        public Task<IEnumerable<BasisOfRecordDto>> GetBasisOfRecordsAsync(CancellationToken cancellationToken = default)
        {
            return _lookupRepository.GetBasisOfRecordsAsync(cancellationToken);
        }
    }
}
