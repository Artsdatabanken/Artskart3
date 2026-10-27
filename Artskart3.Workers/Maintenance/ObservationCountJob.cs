using Artskart3.Core.Application.Maintenance;

namespace Artskart3.Workers.Maintenance;

/// <summary>
/// Fyller <c>ObservationCount</c>-kolonnene etter en import. Port av Artskart 2s
/// <c>updateObservationCounts</c> (plan 4.5 punkt 12).
///
/// EGEN JOBB, ETTER IMPORTEN. Setningene skanner hele <c>Observation</c>, så de hører
/// ikke i importtransaksjonen — men de må kjøre etterpå, ellers viser grensesnittet
/// tallene fra forrige gang. I dag viser det tallene fra Artskart 2-dumpen, fordi
/// ingenting har skrevet dem siden (åpent spørsmål 7.2 punkt 7).
/// </summary>
public class ObservationCountJob
{
    private readonly IObservationCountService _tellinger;
    private readonly ILogger<ObservationCountJob> _logger;

    public ObservationCountJob(IObservationCountService tellinger, ILogger<ObservationCountJob> logger)
    {
        _tellinger = tellinger;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var resultater = await _tellinger.UpdateAllAsync(cancellationToken);

        foreach (var feilet in resultater.Where(x => !x.Succeeded))
        {
            _logger.LogWarning("{Tabell} har fortsatt gamle tellinger: {Feil}", feilet.Table, feilet.Error);
        }
    }
}
