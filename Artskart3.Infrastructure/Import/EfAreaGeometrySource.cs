using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Laster områdene med geometri fra indeksbasen. Én spørring, 4 224 rader, 82,9 MB
/// geometri.
///
/// EGEN SCOPE PER LASTING. Geografitjenesten er en singleton som lever gjennom hele
/// importen, mens en <c>DbContext</c> er scoped og ikke skal holdes åpen så lenge.
/// Samme mønster som <c>AreaHierarchyService</c> bruker.
///
/// LASTER BÅDE GJELDENDE OG HISTORISKE OMRÅDER, men de brukes til ulike ting:
/// koordinatoppslaget i <c>GeographyService</c> filtrerer bort de historiske, mens
/// sentroidtabellen beholder dem som reserve for en post som navngir en kommune som
/// ikke finnes lenger. Derfor filtreres det ikke her.
/// </summary>
public class EfAreaGeometrySource : IAreaGeometrySource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfAreaGeometrySource> _logger;

    public EfAreaGeometrySource(IServiceScopeFactory scopeFactory, ILogger<EfAreaGeometrySource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IReadOnlyList<AreaGeometry> LoadAll()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        // IsRequired hentes fra AreaType. Join i stedet for Include, så vi ikke drar med
        // navigasjonsgrafen på 4 224 rader med store geometrier.
        var rader = context.Set<Area>()
            .AsNoTracking()
            .Where(a => a.WktPolygon != null)
            .Join(
                context.Set<AreaType>().AsNoTracking(),
                a => a.AreaTypeId,
                t => t.Id,
                (a, t) => new
                {
                    a.Id,
                    a.Fid,
                    a.Name,
                    a.AreaTypeId,
                    t.IsRequired,
                    a.IsCurrent,
                    a.ParentFid,
                    a.WktPolygon
                })
            .ToList();

        var områder = rader
            .Where(x => x.WktPolygon is not null)
            .Select(x => new AreaGeometry(
                x.Id,
                x.Fid,
                x.Name,
                x.AreaTypeId,
                x.IsRequired,
                x.IsCurrent,
                x.ParentFid,
                x.WktPolygon!))
            .ToList();

        _logger.LogInformation("Leste {Count} områder med geometri fra indeksbasen", områder.Count);

        return områder;
    }
}
