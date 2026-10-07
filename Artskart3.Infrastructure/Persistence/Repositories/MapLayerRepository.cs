using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class MapLayerRepository : IMapLayerRepository
{
    private readonly IArtsKartDbContext _context;
    private readonly ILogger<MapLayerRepository> _logger;

    public MapLayerRepository(IArtsKartDbContext context, ILogger<MapLayerRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<MapLayerDto>> GetMapLayersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<MapLayer>()
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.Name)
                .Select(l => new MapLayerDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Type = l.Type,
                    Url = l.Url,
                    Layers = l.Layers,
                    Format = l.Format,
                    Version = l.Version,
                    Attribution = l.Attribution
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av kartlag");
            throw new ApplicationException("Feil ved henting av kartlag", ex);
        }
    }
}
