using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Domain.RepositoryInterfaces
{
    public interface IWarningsRepository
    {
        Task<IEnumerable<WarningModel>> GetAllWarningsAsync();
    }
}
