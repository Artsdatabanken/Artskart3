using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface INotificationsRepository
{
    Task<IEnumerable<NotificationModel>> GetAllNotificationsAsync();
}
