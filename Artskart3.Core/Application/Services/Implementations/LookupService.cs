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

        public async Task<AreaResponseDto> GetAreasAsync(CancellationToken cancellationToken = default)
        {
            var areaTypes = await _lookupRepository.GetAreasAsync(cancellationToken);

            // TODO HACK for å skille fastlandsnorge og svalbard, jan mayen, vi bør fikse dette ordentlig når vi setter opp ny import
            return new AreaResponseDto
            {
                Counties = new CountyDto
                {
                    FastlandsNorge = areaTypes.FirstOrDefault(at => at.Id == 2)?.Areas.Where(a => a.Fid != "99" && a.Fid != "22").ToArray(),
                    JanMayen = areaTypes.FirstOrDefault(at => at.Id == 2)?.Areas.FirstOrDefault(a => a.Fid == "22"),
                    Svalbard = areaTypes.FirstOrDefault(at => at.Id == 2)?.Areas.FirstOrDefault(a => a.Fid == "99")
                },
                Municipalities = areaTypes.FirstOrDefault(at => at.Id == 1),
                RestrictedAreas = areaTypes.FirstOrDefault(at => at.Id == 3),
                OceanAreas = areaTypes.FirstOrDefault(at => at.Id == 4),
            };
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
