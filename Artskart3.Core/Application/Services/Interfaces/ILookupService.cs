using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ILookupService
    {
        Task<CategoryListDto> GetCategoriesAsync();
    }
}
