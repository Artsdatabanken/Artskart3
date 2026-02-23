namespace Artskart3.Core.Application.Services.Interfaces
{
    using Artskart3.Core.Application.DTOs;

    public interface IBaseService<TDto> where TDto : BaseDto
    {
        Task<TDto?> GetByIdAsync(int id);
        Task<IEnumerable<TDto>> GetAllAsync();
        Task<TDto> CreateAsync(TDto dto);
        Task<TDto> UpdateAsync(int id, TDto dto);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<int> GetCountAsync();
    }
}
