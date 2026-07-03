namespace Artskart3.Workers.Import;

/// <summary>
/// Hangfire continuation/recurring job — entry point for import.
/// Leser fra cache-databasen, prosesserer og skriver til index-databasen.
/// TODO: Implementeres i neste fase.
/// </summary>
public class ImportJob
{
    private readonly ILogger<ImportJob> _logger;

    public ImportJob(ILogger<ImportJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ImportJob er ikke implementert ennå");
        return Task.CompletedTask;
    }
}
