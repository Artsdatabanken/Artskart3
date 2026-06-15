using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artskart3.Infrastructure.Persistence.Repositories
{
    public class WarningsRepository : IWarningsRepository
    {
        private readonly string _filePath;
        private readonly ILogger<WarningsRepository> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public WarningsRepository(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<WarningsRepository> logger)
        {
            ArgumentNullException.ThrowIfNull(hostEnvironment);
            ArgumentNullException.ThrowIfNull(configuration);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var relativePath = configuration["JsonDb:WarningsPath"]
                ?? throw new InvalidOperationException("JsonDb:WarningsPath is not configured.");

            _filePath = Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, relativePath));
        }

        public async Task<IEnumerable<WarningModel>> GetAllWarningsAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _logger.LogWarning("Warnings file not found at: {FilePath}", _filePath);
                    return Enumerable.Empty<WarningModel>();
                }

                var json = await File.ReadAllTextAsync(_filePath);
                var result = JsonSerializer.Deserialize<WarningsFile>(json, JsonOptions);
                return result?.Warnings ?? Enumerable.Empty<WarningModel>();
            }
            catch (Exception ex) when (ex is not ApplicationException)
            {
                _logger.LogError(ex, "Error reading warnings file at: {FilePath}", _filePath);
                throw new ApplicationException("An error occurred while reading the warnings file.", ex);
            }
        }

        private sealed class WarningsFile
        {
            public List<WarningModel> Warnings { get; set; } = [];
        }
    }
}
