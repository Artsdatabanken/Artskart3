using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Domain.RepositoryInterfaces
{
    public interface ILookupRepository
    {
        Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync();
        Task<IEnumerable<AreaTypeDto>> GetAreasAsync();
        Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync();
    }
}
