using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IWarningsService
{
    Task<IEnumerable<WarningModel>> GetAllWarningsAsync();
}
