using Artskart3.Infrastructure.Services.AlienSpecies;
using Hangfire;

namespace Artskart3.Workers.AlienAssessment;

public sealed class AlienSpeciesAssessmentSyncJob(
    AlienSpeciesAssessmentSyncService syncService)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken) =>
        await syncService.SynchronizeAsync(cancellationToken);
}
