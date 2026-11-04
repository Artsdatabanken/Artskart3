using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Bygger polygondatasettet i bakgrunnen ved oppstart. ÉN gang.
///
/// IKKE BLOKKERENDE MED VILJE
/// Byggingen leser noen millioner rader og tar titalls sekunder. Gjøres den
/// synkront, serverer ikke API-et trafikk før den er ferdig. I stedet svarer
/// <see cref="IPolygonLocationStore.TryQuery"/> null til datasettet står klart,
/// og polygonkallene går mot databasen som før i mellomtiden.
///
/// Feiler byggingen, logges det og API-et fortsetter på databasestien. Et
/// minnelager som ikke kom opp skal ikke ta ned tjenesten.
///
/// INGEN PERIODISK OMBYGGING — OG DET ER IKKE EN FORGLEMMELSE
/// En ombygging i prosessen er ikke trygg med dagens PolygonLocationStore, og en
/// utrygg ombygging er verre enn ingen: lastemetodene skriver rett inn i de
/// parallelle arrayene, mens <c>TryQuery</c> leser dem gjennom hele skanningen.
/// Å nekte NYE lesere adgang holder ikke — en leser som alt er inne i en skanning
/// leser videre, og får rad i fra det nye datasettet og rad j fra det gamle. Noen
/// av arrayene byttes dessuten tidlig i byggingen og andre helt til slutt, så en
/// slik leser kan indeksere et nytt array med gamle offsett. Utfallet er gale
/// antall eller en IndexOutOfRangeException, ikke et tregt svar.
///
/// SKAL DET GJØRES: samle arrayene, oppslagsordbøkene og tellerne i én
/// uforanderlig snapshot-klasse, bygg en ny instans, og publiser den med én
/// volatile referanseskriving. <c>TryQuery</c> leser referansen én gang inn i en
/// lokal variabel. Da serverer det gamle datasettet mens det nye bygges, en
/// mislykket ombygging publiserer ingenting, og leserne ser aldri en blanding.
/// Det er en egen oppgave, ikke en bryter.
///
/// TIL DEN FINNES er datasettet frosset ved prosessoppstart, og en ombygging
/// krever en omstart. Det er akseptabelt så lenge ingenting skriver til
/// Observation eller Location mens appen kjører — ImportJob er ikke implementert,
/// og API-et har ingen skrivesti for observasjoner. Kjøres et manuelt skript mot
/// databasen, må appen startes om for at polygonsøket skal se endringen.
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
