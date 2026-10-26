using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class NotificationsService : INotificationsService
{
    private readonly INotificationsRepository _notificationsRepository;

    public NotificationsService(INotificationsRepository notificationsRepository)
    {
        _notificationsRepository = notificationsRepository ?? throw new ArgumentNullException(nameof(notificationsRepository));
    }

    public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _notificationsRepository.GetAllNotificationsAsync(cancellationToken);
    }
}
