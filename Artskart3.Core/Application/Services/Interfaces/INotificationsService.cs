using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface INotificationsService
{
    Task<IEnumerable<Notification>> GetAllNotificationsAsync();
}
