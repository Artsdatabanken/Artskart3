using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ILookupService
    {
        Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<AreaTypeDto>> GetAreasAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<TaxonGroupDto>> GetTaxonGroupsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<BehaviorDto>> GetBehaviorsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<BasisOfRecordDto>> GetBasisOfRecordsAsync(CancellationToken cancellationToken = default);
    }
}
