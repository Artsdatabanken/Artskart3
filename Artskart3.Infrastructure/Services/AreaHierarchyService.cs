using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Oppslag for områdehierarki (kommune→fylke) og Fid→EntityId-konvertering.
/// Laster data fra Area-tabellen ved oppstart og oppdaterer periodisk.
/// </summary>
public class AreaHierarchyService : IAreaHierarchyService, IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromHours(1);

    private volatile Dictionary<string, string> _municipalityToCounty = new();
    private volatile Dictionary<string, List<string>> _countyToMunicipalities = new();
    private volatile bool _initialized;
    private PeriodicTimer? _timer;
    private Task? _backgroundTask;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AreaHierarchyService> _logger;

    public AreaHierarchyService(IServiceScopeFactory scopeFactory, ILogger<AreaHierarchyService> logger)
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
                _logger.LogError(ex, "Feil ved periodisk oppdatering av områdehierarki");
            }
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        var areas = await context.Set<Area>()
            .Where(a => a.IsCurrent)
            .Select(a => new { a.Fid, a.AreaTypeId, a.ParentFid })
            .Distinct()
            .ToListAsync(cancellationToken);

        var municipalityToCounty = new Dictionary<string, string>();
        var countyToMunicipalities = new Dictionary<string, List<string>>();

        // AreaTypeId 1 = Kommune
        foreach (var m in areas.Where(a => a.AreaTypeId == 1))
        {
            if (string.IsNullOrEmpty(m.ParentFid)) continue;

            municipalityToCounty[m.Fid] = m.ParentFid;

            if (!countyToMunicipalities.TryGetValue(m.ParentFid, out var list))
            {
                list = [];
                countyToMunicipalities[m.ParentFid] = list;
            }

            if (!list.Contains(m.Fid))
                list.Add(m.Fid);
        }

        // Atomisk swap — lesere ser enten gammel eller ny versjon, aldri delvis oppdatert
        _municipalityToCounty = municipalityToCounty;
        _countyToMunicipalities = countyToMunicipalities;
        _initialized = true;

        _logger.LogInformation(
            "Lastet områdehierarki: {MunicipalityCount} kommuner, {CountyCount} fylker",
            municipalityToCounty.Count, countyToMunicipalities.Count);
    }

    public string? GetCountyFid(string municipalityFid)
    {
        EnsureInitialized();
        return _municipalityToCounty.GetValueOrDefault(municipalityFid);
    }

    public IReadOnlyList<string> GetMunicipalityFids(string countyFid)
    {
        EnsureInitialized();
        return _countyToMunicipalities.TryGetValue(countyFid, out var list)
            ? list.AsReadOnly()
            : [];
    }

    public int? FidToEntityId(string fid)
    {
        return int.TryParse(fid.Replace("_", ""), out var id) ? id : null;
    }

    public int? RestrictedAreaFidToEntityId(string fid)
    {
        return int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : null;
    }

    public int[] FidsToEntityIds(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(FidToEntityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    public int[] RestrictedAreaFidsToEntityIds(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(RestrictedAreaFidToEntityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("AreaHierarchyService er ikke initialisert.");
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
