using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ILookupService
{
    Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<AreaResponseDto> GetAreasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OrganizationDto>> SearchOrganizationsAsync(string name, int maxCount, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrganizationDto>> SearchOrganizationsByTypeAsync(string name, int organizationTypeId, int maxCount, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogNumberMatchDto>> SearchCatalogNumbersAsync(string search, int maxCount, CancellationToken cancellationToken = default);
    Task<OrganizationDto?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Et bokmerke lagret av Artskart 2 (#bookmark/&lt;guid&gt;), som filter-JSON slik Artskart 2 skrev det.
    /// </summary>
    Task<string?> GetArtskart2BookmarkAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AreaSearchResultDto>> SearchAreasAsync(string search, int maxCount, CancellationToken cancellationToken = default);
    Task<AreaSearchResultDto?> GetAreaByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CatalogNumberMatchDto?> GetCatalogNumberByObservationIdsAsync(int[] observationIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaxonGroupDto>> GetTaxonGroupsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BehaviorDto>> GetBehaviorsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BasisOfRecordDto>> GetBasisOfRecordsAsync(CancellationToken cancellationToken = default);
}
