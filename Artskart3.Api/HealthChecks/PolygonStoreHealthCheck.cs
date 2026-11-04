using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Artskart3.Api.HealthChecks;

/// <summary>
/// Er polygondatasettet i minnet kommet opp?
///
/// HVA DEN FANGER
/// PolygonLocationStoreBuilder bygger datasettet én gang ved oppstart og SVELGER
/// en feil med vilje — et minnelager som ikke kom opp skal ikke ta ned tjenesten.
/// Prisen var at en mislykket bygging bare etterlot én ERROR-linje ved oppstart.
/// Det finnes ingen periodisk ombygging (se builderen for hvorfor det ikke er
/// trygt med dagens design), så tilstanden er permanent for prosessen: hvert
/// polygonkall går mot databasen til noen starter appen om.
///
/// Reelle feilmåter finnes: byggingen kaster THROW 50001/50002 dersom en geografi-
/// eller prosjektsignatur nærmer seg 4 000 tegn, og byggespørringene har et
/// tidsavbrudd på 1800 sekunder.
///
/// DEGRADED, IKKE UNHEALTHY
/// Databasestien gir RIKTIGE svar, bare tregere — målt 43 ms fra minnet mot
/// hundrevis av millisekunder fra databasen. Samme avveining som
/// Columnstore-sjekken: Unhealthy gir 503 på /hc, og er /hc en readiness-probe,
/// ville en mislykket bygging tatt appen ut av rotasjon. Det hjelper ingen, for
/// en omstart er nettopp det som skal til for å prøve på nytt.
///
/// OPPSTARTSVINDUET RAPPORTERES OGSÅ SOM DEGRADED
/// Byggingen tar rundt 46 sekunder, og sjekken kan ikke skille «bygger nå» fra
/// «feilet»: lageret eksponerer bare IsReady. Det er tålelig fordi Degraded gir
/// 200, men det betyr at et nytt pod-oppstart vises som degradert i under et
/// minutt. Skal de to skilles, må lageret eksponere en tilstand og ikke et flagg.
/// </summary>
public sealed class PolygonStoreHealthCheck : IHealthCheck
{
    private readonly IPolygonLocationStore _store;
    private readonly ILogger<PolygonStoreHealthCheck> _logger;

    public PolygonStoreHealthCheck(IPolygonLocationStore store, ILogger<PolygonStoreHealthCheck> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Describe() er lagerets egen beskrivelse — «75 990 lokasjoner, 3 946 488
        // observasjoner, bygget på 46.0s» når det er klart, «ikke bygget» ellers.
        var beskrivelse = _store.Describe();

        if (_store.IsReady)
        {
            _logger.LogDebug("Polygon store health check passed: {Beskrivelse}", beskrivelse);
            return Task.FromResult(HealthCheckResult.Healthy($"Minnelageret er klart: {beskrivelse}"));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            $"Minnelageret er ikke klart ({beskrivelse}). Bygger det fortsatt, går dette over " +
            "av seg selv i løpet av et minutt. Står det, feilet byggingen — se loggen ved " +
            "oppstart; polygonsøket bruker databasen til appen startes om."));
    }
}
