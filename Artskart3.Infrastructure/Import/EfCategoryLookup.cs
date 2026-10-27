using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Kategorioppslaget, lastet fra indeksbasen ved første bruk. 15 rader.
///
/// Kodene sammenlignes uten hensyn til bokstavstørrelse. Artskart 2 slår opp med
/// koden slik den står i taksonegenskapen, og de er store bokstaver i både
/// <c>TaxonProperty.Tag</c> og <c>Category.Code</c> — men en kilde som skriver «lc»
/// skal ikke gi «ikke vurdert».
/// </summary>
public class EfCategoryLookup : ICategoryLookup
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfCategoryLookup> _logger;
    private readonly Lock _låse = new();

    private Dictionary<string, CategoryRef>? _påKode;

    public EfCategoryLookup(IServiceScopeFactory scopeFactory, ILogger<EfCategoryLookup> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public CategoryRef? ByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        return Last().GetValueOrDefault(code.Trim());
    }

    private Dictionary<string, CategoryRef> Last()
    {
        if (_påKode is not null) return _påKode;

        lock (_låse)
        {
            if (_påKode is not null) return _påKode;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

            var rader = context.Set<Category>()
                .AsNoTracking()
                .Select(x => new { x.Id, x.Code })
                .ToList();

            _påKode = rader
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => new CategoryRef(x.First().Id, x.First().Code),
                    StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Lastet {Count} kategorier", _påKode.Count);

            return _påKode;
        }
    }
}
