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

        public async Task<CategoryListDto> GetCategoriesAsync()
        {

            var result = new CategoryListDto();

            var categoryTypes = await _lookupRepository.GetCategoriesAsync();

            if(categoryTypes != null)
            {
                result.CategoryTypes = categoryTypes!.ToArray();
            }

            return result;
        }
    }
}
