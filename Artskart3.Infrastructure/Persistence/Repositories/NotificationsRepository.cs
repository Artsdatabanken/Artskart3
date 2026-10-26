using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class NotificationsRepository : INotificationsRepository
{
    private readonly IArtsKartDbContext _context;
    public NotificationsRepository(IArtsKartDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        return await _context.Set<Notification>()
            .Where(n => n.StartDisplayDate == null || n.StartDisplayDate <= today)
            .Where(n => n.EndDisplayDate == null || n.EndDisplayDate >= today)
            .Where(n => !n.IsDeleted)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                AlertType = n.AlertType,
                Heading = n.Heading,
                Description = n.Description,
                StartDateTime = n.StartDateTime,
                EndDateTime = n.EndDateTime,
                StartDisplayDate = n.StartDisplayDate,
                EndDisplayDate = n.EndDisplayDate,
                CanClose = n.CanClose
            })
            .ToListAsync();
    }
}
