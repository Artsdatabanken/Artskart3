using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations
{
    public class WarningsService : IWarningsService
    {
        private readonly IWarningsRepository _warningsRepository;

        public WarningsService(IWarningsRepository warningsRepository)
        {
            _warningsRepository = warningsRepository ?? throw new ArgumentNullException(nameof(warningsRepository));
        }

        public async Task<IEnumerable<WarningModel>> GetAllWarningsAsync()
        {
            try
            {
                return await _warningsRepository.GetAllWarningsAsync();
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while retrieving warnings.", ex);
            }
        }
    }
}
