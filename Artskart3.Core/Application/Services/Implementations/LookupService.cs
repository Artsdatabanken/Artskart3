using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

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

        return new AreaResponseDto
        {
            Counties = areaTypes.FirstOrDefault(at => at.Id == (int)AreaType.County),
            Municipalities = areaTypes.FirstOrDefault(at => at.Id == (int)AreaType.Municipality),
            RestrictedAreas = areaTypes.FirstOrDefault(at => at.Id == (int)AreaType.RestrictedArea),
            OceanAreas = areaTypes.FirstOrDefault(at => at.Id == (int)AreaType.OceanArea),
            SvalbardBjørnøyaAndJanMayen = areaTypes.FirstOrDefault(at => at.Id == (int)AreaType.SvalbardBjørnøyaAndJanMayen)
        };
    }

    public Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        return _lookupRepository.GetInstitutionsAsync(cancellationToken);
    }

    public Task<IEnumerable<OrganizationDto>> SearchOrganizationsAsync(string name, int maxCount, CancellationToken cancellationToken = default)
    {
        return _lookupRepository.SearchOrganizationsAsync(name, maxCount, cancellationToken);
    }

    public Task<IEnumerable<OrganizationDto>> SearchOrganizationsByTypeAsync(string name, int organizationTypeId, int maxCount, CancellationToken cancellationToken = default)
    {
        return _lookupRepository.SearchOrganizationsByTypeAsync(name, organizationTypeId, maxCount, cancellationToken);
    }

    public Task<IEnumerable<CatalogNumberMatchDto>> SearchCatalogNumbersAsync(string search, int maxCount, CancellationToken cancellationToken = default)
    {
        return _lookupRepository.SearchCatalogNumbersAsync(search, maxCount, cancellationToken);
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
