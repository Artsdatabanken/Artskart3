using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationsService _notificationsService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationsService notificationsService, ILogger<NotificationsController> logger)
    {
        _notificationsService = notificationsService ?? throw new ArgumentNullException(nameof(notificationsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the notifications whose display period (startDisplayDate / endDisplayDate) includes today.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications(
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationsService.GetAllNotificationsAsync(cancellationToken);
        return Ok(notifications);
    }
}
