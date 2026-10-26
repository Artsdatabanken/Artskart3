using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class NotificationsRepository : INotificationsRepository
{
    private readonly IArtsKartDbContext _context;
    private readonly ILogger<NotificationsRepository> _logger;
    public NotificationsRepository(IArtsKartDbContext context, ILogger<NotificationsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<Notification>> GetAllNotificationsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        return await _context.Set<Notification>()
            .Where(n => n.StartDisplayDate == null || n.StartDisplayDate <= today)
            .Where(n => n.EndDisplayDate == null || n.EndDisplayDate >= today)
            .ToListAsync();
    }
}
