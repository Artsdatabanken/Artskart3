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

        var result = new List<TaxonTreeNodeDto>(childIds.Count);
        foreach (var childId in childIds)
        {
            if (!_taxons.TryGetValue(childId, out var taxon)) continue;
            if (!IsVisible(taxon)) continue;

            result.Add(new TaxonTreeNodeDto
            {
                Id = taxon.Id,
                ValidScientificName = taxon.ValidScientificName,
                PreferredPopularName = taxon.PreferredPopularName,
                TaxonRankId = taxon.TaxonRankId,
                TaxonGroupId = taxon.TaxonGroupId,
                CumulativeObservationCount = taxon.CumulativeObservationCount,
                ExistsInCountry = taxon.ExistsInCountry,
                HasChildren = _childrenByParent.ContainsKey(taxon.Id)
            });
        }

        return result.OrderBy(t => t.ValidScientificName).ToList();
    }

    public List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds)
    {
        EnsureInitialized();

        var result = new List<TaxonAncestryDto>();
        foreach (var taxonId in taxonIds.Distinct())
        {
            var parentIds = BuildParentIdChain(taxonId);

            // Synlige barn for hvert nivå i kjeden, inkludert taxonet selv — lar frontend
            // avgjøre full dekning uten at treet er ekspandert ned til nivået.
            var levels = new List<TaxonAncestryLevelDto>(parentIds.Count + 1);
            foreach (var levelId in parentIds.Append(taxonId))
            {
                levels.Add(new TaxonAncestryLevelDto
                {
                    ParentId = levelId,
                    ChildIds = GetVisibleChildIds(levelId)
                });
            }

            result.Add(new TaxonAncestryDto
            {
                Id = taxonId,
                ParentIds = parentIds,
                Levels = levels
            });
        }

        return result;
    }

    /// <summary>
    /// Synlige barn av en forelder, filtrert likt som GetChildren.
    /// </summary>
    private List<int> GetVisibleChildIds(int parentId)
    {
        if (!_childrenByParent.TryGetValue(parentId, out var childIds)) return [];

        var result = new List<int>(childIds.Count);
        foreach (var childId in childIds)
        {
            if (_taxons.TryGetValue(childId, out var taxon) && IsVisible(taxon))
                result.Add(childId);
        }

        return result;
    }

    /// <summary>
    /// Kun taxoner med observasjoner eller som finnes i landet vises i treet.
    /// </summary>
    private static bool IsVisible(TaxonData taxon) =>
        taxon.CumulativeObservationCount is not (null or 0) || taxon.ExistsInCountry;

    /// <summary>
    /// Bygger foreldrekjeden for et taxonId som id-er, sortert fra rotnivå til nærmeste forelder.
    /// Returnerer tom liste for rotnivå eller ukjent taxonId.
    /// Foreldre filtreres ikke på observasjonsantall — hierarkiet skal alltid være komplett.
    /// </summary>
    private List<int> BuildParentIdChain(int taxonId)
    {
        if (!_taxons.TryGetValue(taxonId, out var taxon)) return [];

        var chain = new List<int>();
        var visited = new HashSet<int> { taxonId };
        var currentId = taxon.ParentTaxonId;

        // visited beskytter mot sykluser dersom ParentTaxonId-dataene er korrupte
        while (currentId != 0 && visited.Add(currentId))
        {
            chain.Add(currentId);
            currentId = _taxons.TryGetValue(currentId, out var parent) ? parent.ParentTaxonId : 0;
        }

        chain.Reverse();
        return chain;
    }

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
