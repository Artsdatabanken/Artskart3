using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class NotificationsRepository : INotificationsRepository
{
    private readonly string _filePath;
    private readonly ILogger<NotificationsRepository> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public NotificationsRepository(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<NotificationsRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var relativePath = configuration["JsonDb:NotificationsPath"]
            ?? throw new InvalidOperationException("JsonDb:NotificationsPath is not configured.");

        _filePath = Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, relativePath));
    }

    public async Task<IEnumerable<NotificationModel>> GetAllNotificationsAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("Notifications file not found at: {FilePath}", _filePath);
                return Enumerable.Empty<NotificationModel>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var result = JsonSerializer.Deserialize<NotificationsFile>(json, JsonOptions);
            return result?.Notifications ?? Enumerable.Empty<NotificationModel>();
        }
        catch (Exception ex) when (ex is not ApplicationException)
        {
            _logger.LogError(ex, "Error reading notifications file at: {FilePath}", _filePath);
            throw new ApplicationException("An error occurred while reading the notifications file.", ex);
        }
    }

    private sealed class NotificationsFile
    {
        public List<NotificationModel> Notifications { get; set; } = [];
    }
}
