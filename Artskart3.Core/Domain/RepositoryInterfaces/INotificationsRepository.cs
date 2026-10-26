using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface INotificationsRepository
{
    Task<IEnumerable<Notification>> GetAllNotificationsAsync();
}
