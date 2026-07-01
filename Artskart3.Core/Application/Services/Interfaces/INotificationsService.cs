using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface INotificationsService
{
    Task<IEnumerable<NotificationModel>> GetAllNotificationsAsync();
}
