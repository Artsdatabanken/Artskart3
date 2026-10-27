using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Datakilderegisteret lest fra indeksbasen. Spør hver gang: tabellen er 39 rader, og
/// konfigurasjonen skal være den som gjelder nå — ikke den som gjaldt da prosessen
/// startet.
/// </summary>
public class EfDataSourceRegistry : IDataSourceRegistry
{
    private readonly IArtsKartDbContext _context;

    public EfDataSourceRegistry(IArtsKartDbContext context)
    {
        _context = context;
    }

    public async Task<DataSourceConfig?> ByNodeIdAsync(int nodeId, CancellationToken cancellationToken = default)
    {
        var rad = await _context.Set<DataSource>()
            .AsNoTracking()
            .Where(x => x.Id == nodeId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsSensitive,
                x.GbifDataSetId,
                x.GbifDoi,
                x.ImportPropertyOverrides,
                x.NonValidOccuranceIds
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (rad is null) return null;

        // Kaster for en ugyldig konfigurasjon, og den skal kaste her: noden skal ikke
        // begynne å kjøre med overstyringer som ikke kan anvendes.
        var overstyringer = ImportPropertyOverrides.Parse(rad.ImportPropertyOverrides);

        return new DataSourceConfig(
            rad.Id,
            rad.Name,
            rad.IsSensitive,
            rad.GbifDataSetId,
            rad.GbifDoi,
            overstyringer,
            rad.NonValidOccuranceIds);
    }

    public async Task<IReadOnlyList<DataSourceSummary>> ActiveNodesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<DataSource>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => new DataSourceSummary(x.Id, x.Name, x.IsSensitive, x.UpdateFrequencyInDays))
            .ToListAsync(cancellationToken);
    }
}
