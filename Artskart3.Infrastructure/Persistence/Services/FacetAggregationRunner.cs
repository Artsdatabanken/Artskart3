using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// Kjører fasett-aggregeringene samtidig, hver på sin egen DbContext.
///
/// HVORFOR EGNE KONTEKSTER OG IKKE EN DbContextFactory
/// <c>AddDbContext</c> registrerer allerede <see cref="DbContextOptions{TContext}"/>
/// i containeren. Å legge til <c>AddDbContextFactory</c> ved siden av ville ha
/// kollidert om den registreringen og tvunget fram en endring i hvordan HELE
/// applikasjonens context lages — for en sti som bare gjelder fasett-tall. Her
/// gjenbrukes de samme innstillingene direkte, så hovedregistreringen står urørt.
///
/// HVORFOR ET EGET REPOSITORY PER JOBB
/// <see cref="SearchRepository"/> holder sin context som felt. En context tåler bare
/// én operasjon om gangen, så parallelle jobber må ha hvert sitt par. Alt annet
/// repositoriet trenger — hierarkitjenestene, matrikkeltjenesten — er trygt å dele.
///
/// Bufferne sendes bevisst ikke med: denne stien ER fall-through-en. Hadde de vært
/// med, ville hver jobb slått opp i en buffer som nettopp har svart nei.
/// </summary>
public class FacetAggregationRunner : IFacetAggregationRunner
{
    private readonly DbContextOptions<ArtskartDbContext> _contextOptions;
    private readonly ILogger<SearchRepository> _repositoryLogger;
    private readonly IOptions<PaginationOptions> _paginationOptions;
    private readonly IAreaHierarchyService _areaHierarchy;
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly ICadastralUnitService _cadastralUnits;
    private readonly FacetCountOptions _options;

    public FacetAggregationRunner(
        DbContextOptions<ArtskartDbContext> contextOptions,
        ILogger<SearchRepository> repositoryLogger,
        IOptions<PaginationOptions> paginationOptions,
        IAreaHierarchyService areaHierarchy,
        ITaxonHierarchyService taxonHierarchy,
        ICadastralUnitService cadastralUnits,
        IOptions<FacetCountOptions> options)
    {
        _contextOptions = contextOptions;
        _repositoryLogger = repositoryLogger;
        _paginationOptions = paginationOptions;
        _areaHierarchy = areaHierarchy;
        _taxonHierarchy = taxonHierarchy;
        _cadastralUnits = cadastralUnits;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Dictionary<int, int>>> AggregateDimensionsAsync(
        IObservationFilter filter,
        IReadOnlyList<FacetDimension> facets,
        FilterMask.Selection mask,
        int[]? cadastralLocationIds,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            facets,
            (repository, facet, token) =>
                repository.AggregateFacetCountsAsync(filter, facet, mask, cadastralLocationIds, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<int>> AggregateTotalsAsync(
        IObservationFilter filter,
        IReadOnlyList<FilterMask.Selection> masks,
        int[]? cadastralLocationIds,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            masks,
            (repository, maskeutvalg, token) =>
                repository.AggregateFacetTotalAsync(filter, maskeutvalg, cadastralLocationIds, token),
            cancellationToken);

    /// <summary>
    /// Kjører én jobb per element, maks <see cref="FacetCountOptions.MaxConcurrency"/>
    /// om gangen, og beholder rekkefølgen.
    ///
    /// Ett element kjøres uten semafor og uten egen context — det vanligste tilfellet
    /// er at bare én gruppe mangler tall, og da er det ingenting å overlappe.
    /// </summary>
    private async Task<IReadOnlyList<TResult>> RunAsync<TInput, TResult>(
        IReadOnlyList<TInput> inputs,
        Func<SearchRepository, TInput, CancellationToken, Task<TResult>> job,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
            return [];

        var concurrency = Math.Max(1, _options.MaxConcurrency);

        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var tasks = inputs.Select(async input =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Egen context per jobb, og den lukkes med én gang jobben er ferdig.
                // Semaforen slipper neste jobb inn først etterpå, så antall åpne
                // tilkoblinger er aldri høyere enn taket.
                await using var context = new ArtskartDbContext(_contextOptions);

                var repository = new SearchRepository(
                    context,
                    _repositoryLogger,
                    _paginationOptions,
                    _areaHierarchy,
                    _taxonHierarchy,
                    _cadastralUnits);

                return await job(repository, input, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }
}
