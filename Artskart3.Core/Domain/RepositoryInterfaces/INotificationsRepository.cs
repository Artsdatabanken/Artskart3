using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface INotificationsRepository
{
    Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync();
}
