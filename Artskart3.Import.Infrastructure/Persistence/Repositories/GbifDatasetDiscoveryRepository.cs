using Artskart3.Import.Core.Domain.Entities;
using Artskart3.Import.Core.Domain.Enums;
using Artskart3.Import.Core.Domain.RepositoryInterfaces;
using Artskart3.Import.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Import.Infrastructure.Persistence.Repositories;

public class GbifDatasetDiscoveryRepository : IGbifDatasetDiscoveryRepository
{
    private readonly ArtskartImportDbContext _context;

    public GbifDatasetDiscoveryRepository(ArtskartImportDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GbifDatasetDiscovery>> GetAllAsync()
        => await _context.GbifDatasetDiscoveries.ToListAsync();

    public async Task<IEnumerable<GbifDatasetDiscovery>> GetByStatusAsync(DiscoveryStatus status)
        => await _context.GbifDatasetDiscoveries.Where(d => d.Status == status).ToListAsync();

    public async Task<GbifDatasetDiscovery?> GetByIdAsync(int id)
        => await _context.GbifDatasetDiscoveries.FindAsync(id);

    public async Task<GbifDatasetDiscovery?> GetByGbifIdAsync(string gbifId)
        => await _context.GbifDatasetDiscoveries.FirstOrDefaultAsync(d => d.GbifId == gbifId);

    public async Task AddAsync(GbifDatasetDiscovery discovery)
    {
        discovery.CreatedAt = DateTime.UtcNow;
        discovery.UpdatedAt = DateTime.UtcNow;
        await _context.GbifDatasetDiscoveries.AddAsync(discovery);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(GbifDatasetDiscovery discovery)
    {
        discovery.UpdatedAt = DateTime.UtcNow;
        _context.GbifDatasetDiscoveries.Update(discovery);
        await _context.SaveChangesAsync();
    }
}
