using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Oppslag for taksonhierarki.
/// Laster alle aktive taxoner ved oppstart og oppdaterer periodisk.
/// Brukes av SearchRepository (rangoppslag) og LookupController (trevisning).
/// </summary>
public class TaxonHierarchyService : ITaxonHierarchyService, IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromHours(1);

    private volatile Dictionary<int, int> _taxonRanks = new();
    private volatile Dictionary<int, TaxonData> _taxons = new();
    private volatile Dictionary<int, List<int>> _childrenByParent = new();
    private volatile bool _initialized;
    private PeriodicTimer? _timer;
    private Task? _backgroundTask;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaxonHierarchyService> _logger;

    public TaxonHierarchyService(IServiceScopeFactory scopeFactory, ILogger<TaxonHierarchyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        _timer = new PeriodicTimer(ReloadInterval);
        _backgroundTask = ReloadLoopAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        if (_backgroundTask != null)
            await _backgroundTask;
    }

    private async Task ReloadLoopAsync()
    {
        while (_timer != null && await _timer.WaitForNextTickAsync())
        {
            try
            {
                await LoadAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved periodisk oppdatering av taksonhierarki");
            }
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        var taxonList = await context.Set<Taxon>()
            .Where(t => !t.IsDeleted)
            .Select(t => new
            {
                t.Id,
                t.TaxonRankId,
                t.ParentTaxonId,
                t.TaxonGroupId,
                t.ValidScientificName,
                t.PreferredPopularName,
                t.CumulativeObservationCount,
                t.ExistsInCountry
            })
            .ToListAsync(cancellationToken);

        var taxonRanks = new Dictionary<int, int>(taxonList.Count);
        var taxons = new Dictionary<int, TaxonData>(taxonList.Count);
        var childrenByParent = new Dictionary<int, List<int>>();

        foreach (var t in taxonList)
        {
            taxonRanks[t.Id] = t.TaxonRankId;
            taxons[t.Id] = new TaxonData
            {
                Id = t.Id,
                TaxonRankId = t.TaxonRankId,
                ParentTaxonId = t.ParentTaxonId,
                TaxonGroupId = t.TaxonGroupId,
                ValidScientificName = t.ValidScientificName,
                PreferredPopularName = t.PreferredPopularName,
                CumulativeObservationCount = t.CumulativeObservationCount,
                ExistsInCountry = t.ExistsInCountry
            };

            if (t.ParentTaxonId == 0) continue;

            if (!childrenByParent.TryGetValue(t.ParentTaxonId, out var list))
            {
                list = [];
                childrenByParent[t.ParentTaxonId] = list;
            }
            list.Add(t.Id);
        }

        _taxonRanks = taxonRanks;
        _taxons = taxons;
        _childrenByParent = childrenByParent;
        _initialized = true;

        _logger.LogInformation(
            "Lastet taksonhierarki: {TaxonCount} taxoner",
            taxonList.Count);
    }

    public int? GetTaxonRankId(int taxonId)
    {
        EnsureInitialized();
        return _taxonRanks.TryGetValue(taxonId, out var rankId) ? rankId : null;
    }

    public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId)
    {
        EnsureInitialized();

        var parentId = parentTaxonId ?? 0;
        List<int>? childIds;

        if (parentId == 0)
        {
            // Rotnoder: taxoner med ParentTaxonId = 0
            childIds = _taxons.Values
                .Where(t => t.ParentTaxonId == 0)
                .Select(t => t.Id)
                .ToList();
        }
        else if (!_childrenByParent.TryGetValue(parentId, out childIds))
        {
            return [];
        }

        // Alle barna deler samme forelderkjede — bygg den én gang per forespørsel
        var parentChain = BuildParentChain(parentId);

        var result = new List<TaxonTreeNodeDto>(childIds.Count);
        foreach (var childId in childIds)
        {
            if (!_taxons.TryGetValue(childId, out var taxon)) continue;

            // Filtrer: kun taxoner med observasjoner eller som finnes i landet
            if (taxon.CumulativeObservationCount is null or 0 && !taxon.ExistsInCountry) continue;

            var node = ToDto(taxon);
            // Egen liste per node slik at kallere ikke deler samme instans
            node.Parents = [.. parentChain];
            result.Add(node);
        }

        return result.OrderBy(t => t.ValidScientificName).ToList();
    }

    /// <summary>
    /// Bygger hele forelderkjeden for et taxonId, sortert fra rotnivå til og med taxonet selv.
    /// Returnerer tom liste for rotnivå (taxonId 0) eller ukjent taxonId.
    /// Foreldre filtreres ikke på observasjonsantall — hierarkiet skal alltid være komplett.
    /// </summary>
    private List<TaxonTreeNodeDto> BuildParentChain(int taxonId)
    {
        if (taxonId == 0) return [];

        var chain = new List<TaxonTreeNodeDto>();
        var visited = new HashSet<int>();
        var currentId = taxonId;

        // visited beskytter mot sykluser dersom ParentTaxonId-dataene er korrupte
        while (currentId != 0 && visited.Add(currentId) && _taxons.TryGetValue(currentId, out var taxon))
        {
            chain.Add(ToDto(taxon));
            currentId = taxon.ParentTaxonId;
        }

        chain.Reverse();
        return chain;
    }

    private TaxonTreeNodeDto ToDto(TaxonData taxon) => new()
    {
        Id = taxon.Id,
        ValidScientificName = taxon.ValidScientificName,
        PreferredPopularName = taxon.PreferredPopularName,
        TaxonRankId = taxon.TaxonRankId,
        TaxonGroupId = taxon.TaxonGroupId,
        CumulativeObservationCount = taxon.CumulativeObservationCount,
        ExistsInCountry = taxon.ExistsInCountry,
        HasChildren = _childrenByParent.ContainsKey(taxon.Id)
    };

    public List<int> GetDescendantSpeciesIds(int taxonId)
    {
        EnsureInitialized();
        var result = new List<int>();
        CollectDescendantSpecies(taxonId, result);
        return result;
    }

    public List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId)
    {
        EnsureInitialized();
        var result = new List<int>();
        CollectDescendantsAtRank(taxonId, targetRankId, result);
        return result;
    }

    private void CollectDescendantSpecies(int taxonId, List<int> result)
    {
        if (_taxons.TryGetValue(taxonId, out var taxon) && taxon.TaxonRankId == 22)
        {
            result.Add(taxonId);
        }

        if (_childrenByParent.TryGetValue(taxonId, out var childIds))
        {
            foreach (var childId in childIds)
            {
                CollectDescendantSpecies(childId, result);
            }
        }
    }

    private void CollectDescendantsAtRank(int taxonId, int targetRankId, List<int> result)
    {
        if (_taxons.TryGetValue(taxonId, out var taxon) && taxon.TaxonRankId == targetRankId)
        {
            result.Add(taxonId);
            return; // Ikke gå dypere — vi har funnet riktig nivå
        }

        if (_childrenByParent.TryGetValue(taxonId, out var childIds))
        {
            foreach (var childId in childIds)
            {
                CollectDescendantsAtRank(childId, targetRankId, result);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("TaxonHierarchyService er ikke initialisert.");
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private class TaxonData
    {
        public int Id { get; init; }
        public int TaxonRankId { get; init; }
        public int ParentTaxonId { get; init; }
        public int TaxonGroupId { get; init; }
        public string? ValidScientificName { get; init; }
        public string? PreferredPopularName { get; init; }
        public int? CumulativeObservationCount { get; init; }
        public bool ExistsInCountry { get; init; }
    }
}
