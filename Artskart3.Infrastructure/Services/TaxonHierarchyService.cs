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
/// Laster TaxonRankId for alle taxoner ved oppstart og oppdaterer periodisk.
/// Brukes av SearchRepository for å bestemme hvilken kolonne i ObservationTaxonHierarchy som skal spørres.
/// </summary>
public class TaxonHierarchyService : ITaxonHierarchyService, IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromHours(1);

    private volatile Dictionary<int, int> _taxonRanks = new();
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

        var taxons = await context.Set<Taxon>()
            .Where(t => !t.IsDeleted)
            .Select(t => new { t.Id, t.TaxonRankId })
            .ToListAsync(cancellationToken);

        var taxonRanks = new Dictionary<int, int>(taxons.Count);
        foreach (var t in taxons)
        {
            taxonRanks[t.Id] = t.TaxonRankId;
        }

        _taxonRanks = taxonRanks;
        _initialized = true;

        _logger.LogInformation(
            "Lastet taksonhierarki: {TaxonCount} taxoner",
            taxons.Count);
    }

    public int? GetTaxonRankId(int taxonId)
    {
        EnsureInitialized();
        return _taxonRanks.TryGetValue(taxonId, out var rankId) ? rankId : null;
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
}
