using System.Diagnostics;
using Artskart3.Core.Application.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Henter lokasjonene uten områder (se <see cref="CustomAreaService.LoadLocationsWithoutAreasAsync"/>)
/// ved oppstart og fornyer dem før de går ut av bufferen. Ellers venter første egendefinerte
/// område med et helt område innenfor på spørringen: målt 5-7 s for det oppslaget, mot 2-3 s
/// for de neste.
///
/// Holder ikke igjen oppstarten. Feiler hentingen, logges det og forsøkes igjen senere, og
/// første egendefinerte område henter dem selv.
/// </summary>
public sealed class LocationsWithoutAreasWarmup(
    IServiceScopeFactory scopes, IMemoryCache cache, ILogger<LocationsWithoutAreasWarmup> logger) : BackgroundService
{
    // Halve bufferens levetid, så listen aldri går ut mens appen kjører.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = RefreshInterval;
            try
            {
                var started = Stopwatch.GetTimestamp();
                using var scope = scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();
                var rows = await CustomAreaService.LoadLocationsWithoutAreasAsync(context, stoppingToken);
                CustomAreaService.CacheLocationsWithoutAreas(cache, rows);
                logger.LogInformation("Lokasjoner uten områder: {Antall} hentet på {Sek:F1}s",
                    rows.Count, Stopwatch.GetElapsedTime(started).TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Henting av lokasjoner uten områder feilet. Første egendefinerte område henter dem selv.");
                wait = RetryInterval;
            }

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
