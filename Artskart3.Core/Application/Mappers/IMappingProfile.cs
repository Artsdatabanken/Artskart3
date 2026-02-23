namespace Artskart3.Core.Application.Mappers
{
    public interface IMappingProfile
    {
        TDto MapToDto<TEntity, TDto>(TEntity entity) 
            where TEntity : class 
            where TDto : class;
        TEntity MapToEntity<TDto, TEntity>(TDto dto) 
            where TDto : class 
            where TEntity : class;
    }
}
