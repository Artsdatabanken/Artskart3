using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface INotificationsService
{
    Task<IEnumerable<NotificationDto>> GetAllActiveNotificationsAsync(CancellationToken cancellationToken = default);
}
