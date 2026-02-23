namespace Artskart3.Core.Application.Services.Implementations
{
    using Artskart3.Core.Application.DTOs;
    using Artskart3.Core.Application.Mappers;
    using Artskart3.Core.Application.Services.Interfaces;
    using Artskart3.Core.Domain.Entities;
    using Artskart3.Core.Domain.RepositoryInterfaces;

    public abstract class BaseService<TDto> : IBaseService<TDto> where TDto : BaseDto
    {
        protected readonly IBaseRepository<BaseEntity> _repository;
        protected readonly IMappingProfile _mapper;

        public BaseService(IBaseRepository<BaseEntity> repository, IMappingProfile mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public virtual async Task<TDto?> GetByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return null;

            return _mapper.MapToDto<BaseEntity, TDto>(entity);
        }

        public virtual async Task<IEnumerable<TDto>> GetAllAsync()
        {
            var entities = await _repository.GetAllAsync();
            return entities.Select(e => _mapper.MapToDto<BaseEntity, TDto>(e));
        }

        public virtual async Task<TDto> CreateAsync(TDto dto)
        {
            var entity = _mapper.MapToEntity<TDto, BaseEntity>(dto);
            var createdEntity = await _repository.AddAsync(entity);
            return _mapper.MapToDto<BaseEntity, TDto>(createdEntity);
        }

        public virtual async Task<TDto> UpdateAsync(int id, TDto dto)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                throw new InvalidOperationException($"Entity with ID {id} not found.");

            var updatedEntity = _mapper.MapToEntity<TDto, BaseEntity>(dto);
            updatedEntity.Id = id;
            var result = await _repository.UpdateAsync(updatedEntity);
            return _mapper.MapToDto<BaseEntity, TDto>(result);
        }

        public virtual async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await _repository.ExistsAsync(id);
        }

        public virtual async Task<int> GetCountAsync()
        {
            return await _repository.CountAsync();
        }
    }
}
