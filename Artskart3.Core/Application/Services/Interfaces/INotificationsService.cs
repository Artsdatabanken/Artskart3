using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface INotificationsService
{
    /// <summary>Returns the notifications whose display period includes today.</summary>
    Task<IEnumerable<Notification>> GetAllNotificationsAsync();
}
