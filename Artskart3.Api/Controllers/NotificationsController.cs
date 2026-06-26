using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
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
    /// Returns all configured notifications.
    /// The client filters by startDisplayDate / endDisplayDate to determine which notifications to show.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<NotificationModel>>> GetNotifications(CancellationToken cancellationToken = default)
    {
        try
        {
            var notifications = await _notificationsService.GetAllNotificationsAsync();
            return Ok(notifications);
        }
        catch (ApplicationException ex)
        {
            _logger.LogWarning(ex, "Application error retrieving notifications");
            return StatusCode(503, new { error = "An error occurred while retrieving notifications. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving notifications");
            return StatusCode(500, new { error = "An unexpected error occurred. Please try again later." });
        }
    }
}
