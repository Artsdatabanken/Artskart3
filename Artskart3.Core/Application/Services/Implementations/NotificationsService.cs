using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class NotificationsService : INotificationsService
{
    private readonly INotificationsRepository _notificationsRepository;

    public NotificationsService(INotificationsRepository notificationsRepository)
    {
        _notificationsRepository = notificationsRepository ?? throw new ArgumentNullException(nameof(notificationsRepository));
    }

    public async Task<IEnumerable<NotificationModel>> GetAllNotificationsAsync()
    {
        try
        {
            return await _notificationsRepository.GetAllNotificationsAsync();
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving notifications.", ex);
        }
    }
}
