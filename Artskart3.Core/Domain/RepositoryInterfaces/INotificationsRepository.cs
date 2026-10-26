using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface INotificationsRepository
{
    /// <summary>Returns the notifications whose display period includes today. A missing start or end date means open-ended.</summary>
    Task<IEnumerable<Notification>> GetAllNotificationsAsync();
}
