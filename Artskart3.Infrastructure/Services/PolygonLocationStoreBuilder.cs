using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Bygger polygondatasettet i bakgrunnen ved oppstart.
///
/// IKKE BLOKKERENDE MED VILJE
/// Byggingen leser noen millioner rader og tar titalls sekunder. Gjøres den
/// synkront, serverer ikke API-et trafikk før den er ferdig. I stedet svarer
/// <see cref="IPolygonLocationStore.TryQuery"/> null til datasettet står klart,
/// og polygonkallene går mot databasen som før i mellomtiden.
///
/// Feiler byggingen, logges det og API-et fortsetter på databasestien. Et
/// minnelager som ikke kom opp skal ikke ta ned tjenesten.
/// </summary>
public sealed class PolygonLocationStoreBuilder : BackgroundService
{
    private readonly IPolygonLocationStore _store;
    private readonly ILogger<PolygonLocationStoreBuilder> _logger;

    public PolygonLocationStoreBuilder(
        IPolygonLocationStore store, ILogger<PolygonLocationStoreBuilder> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Slipper oppstarten fram først. Uten dette konkurrerer byggingen med
        // alt annet som initialiseres.
        await Task.Yield();

        try
        {
            _logger.LogInformation("Bygger polygondatasettet i minnet...");
            await _store.BuildAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Bygging av polygondatasettet avbrutt ved nedstenging.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Bygging av polygondatasettet feilet. Polygonsøket bruker databasen som før.");
        }
    }
}
