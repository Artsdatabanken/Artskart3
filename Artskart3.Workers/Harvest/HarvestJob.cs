namespace Artskart3.Workers.Harvest;

/// <summary>
/// Hangfire recurring job — entry point for harvest.
/// Henter data fra eksterne datakilder og skriver til cache-databasen.
/// TODO: Implementeres i neste fase.
/// </summary>
public class HarvestJob
{
    private readonly ILogger<HarvestJob> _logger;

    public HarvestJob(ILogger<HarvestJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HarvestJob er ikke implementert ennå");
        return Task.CompletedTask;
    }
}
