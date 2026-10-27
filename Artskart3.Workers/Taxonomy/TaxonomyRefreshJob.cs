namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Hangfire-inngangen til taksonomispeilingen. Tynn med vilje; arbeidet ligger i
/// <see cref="TaxonomyRefreshService"/>.
///
/// IKKE REGISTRERT SOM TILBAKEVENDENDE JOBB. Speilingen leser fra Artskart 2s RavenDB,
/// og mot riggen skal den peke på den FROSNE kopien — ikke den i drift. Den startes
/// derfor med vilje, ikke av en cron.
///
/// MIDLERTIDIG. Hele denne mappen skal slettes når harvest-delen flytter dokumentene
/// til SQL Server. Da er det én mappe og én jobbregistrering som forsvinner.
/// </summary>
public class TaxonomyRefreshJob
{
    private readonly TaxonomyRefreshService _tjeneste;
    private readonly ILogger<TaxonomyRefreshJob> _logger;

    public TaxonomyRefreshJob(TaxonomyRefreshService tjeneste, ILogger<TaxonomyRefreshJob> logger)
    {
        _tjeneste = tjeneste;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var resultat = await _tjeneste.RefreshAsync(cancellationToken);

        _logger.LogInformation(
            "Taksonomien speilet fra {Kilde}: {Taksoner} taksoner, {Navn} navn",
            resultat.Source, resultat.Taxa, resultat.Names);
    }
}
