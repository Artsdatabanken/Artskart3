namespace Artskart3.Infrastructure.Services.AlienSpecies;

public interface IAlienSpeciesAssessmentClient
{
    Task<TaxonAssessmentFetchResult<CurrentAlienSpeciesAssessment>> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<TaxonAssessmentFetchResult<AlienSpeciesAssessment2023>> GetSnapshot2023Async(
        CancellationToken cancellationToken = default);
}
