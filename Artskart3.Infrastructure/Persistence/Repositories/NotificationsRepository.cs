using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class NotificationsRepository : INotificationsRepository
{
    private static readonly TimeZoneInfo NorwegianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    private readonly IArtsKartDbContext _context;
    private readonly TimeProvider _timeProvider;

    public NotificationsRepository(IArtsKartDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IEnumerable<NotificationDto>> GetAllActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var nowNorway = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), NorwegianTimeZone).DateTime;

        return await _context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.PublishFrom == null || n.PublishFrom <= nowNorway)
            .Where(n => n.PublishUntil == null || n.PublishUntil >= nowNorway)
            .Where(n => !n.IsDeleted)
            .OrderBy(n => n.AlertType)
            .ThenBy(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                AlertType = n.AlertType,
                Heading = n.Heading,
                Description = n.Description,
                PublishFrom = n.PublishFrom,
                PublishUntil = n.PublishUntil,
                EventStart = n.EventStart,
                EventEnd = n.EventEnd,
                CanClose = n.CanClose
            })
            .ToListAsync(cancellationToken);
    }
}
