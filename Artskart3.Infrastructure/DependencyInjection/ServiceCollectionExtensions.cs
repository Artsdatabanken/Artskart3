using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Cache;
using Artskart3.Infrastructure.Import;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Persistence.Services;
using Artskart3.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<IObservationDetailsRepository, ObservationDetailsRepository>();
        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IMapLayerRepository, MapLayerRepository>();
        // Add other repositories here
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<AreaHierarchyService>();
        services.AddSingleton<IAreaHierarchyService>(sp => sp.GetRequiredService<AreaHierarchyService>());
        services.AddHostedService(sp => sp.GetRequiredService<AreaHierarchyService>());
        services.AddSingleton<TaxonHierarchyService>();
        services.AddSingleton<ITaxonHierarchyService>(sp => sp.GetRequiredService<TaxonHierarchyService>());
        services.AddHostedService(sp => sp.GetRequiredService<TaxonHierarchyService>());
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IObservationDetailsService, ObservationDetailsService>();
        services.AddHttpClient(ObservationMapService.ClientName, client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<IObservationMapService, ObservationMapService>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IMapLayerService, MapLayerService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<ISavedFilterService, SavedFilterService>();
        services.AddSingleton<ExportColumnRegistry>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        // Add other application services here
        return services;
    }

    /// <summary>
    /// Lesestien mot Artskart 2s cache-database. Egen kontekst og egen
    /// tilkoblingsstreng (<c>ConnectionStrings:ArtskartCache</c>) fordi cachen er en
    /// annen database med en annen eier — den skal leses, aldri skrives.
    ///
    /// Registreres bare der den brukes, altså i Artskart3.Workers. API-et har ingenting
    /// å gjøre i cachen.
    /// </summary>
    public static IServiceCollection AddCacheReader(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<ArtskartCacheDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql => sql.CommandTimeout(600));

            // Cache-skjemaet eies av EF6-modellen i Artskart 2 og er ikke under
            // migrasjonskontroll her. Uten dette advarer EF om ventende
            // modellendringer for en kontekst som aldri skal migrere noe.
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<ICacheRecordReader, CacheRecordReader>();

        return services;
    }

    /// <summary>
    /// Tolkningstjenestene importen bruker.
    ///
    /// Alle er singleton fordi de holder referansedata i minnet: 4 224
    /// områdegeometrier (319 MB) og hele taksonomien (152 756 taksoner, 228 639 navn).
    /// Oppslagene leser fra indeksbasen én gang, ved første bruk — en worker som ikke
    /// importerer betaler ingenting.
    /// </summary>
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        // Transformasjonene bufres statisk og er tråd-trygge etter opprettelse.
        services.AddSingleton<ICoordinateTransformer, ProjNetCoordinateTransformer>();

        // Geografitjenesten holder 4 224 områdegeometrier i minnet og må derfor være
        // singleton — én kopi per prosess, lastet ved første oppslag.
        services.AddSingleton<IAreaGeometrySource, EfAreaGeometrySource>();
        services.AddSingleton<IGeographyService, GeographyService>();

        // Navneoppslaget holder hele taksonomien i minnet og er singleton av samme
        // grunn som geografitjenesten.
        services.AddSingleton<ITaxonomySource, EfTaxonomySource>();
        services.AddSingleton<ITaxonNameResolver, TaxonNameResolver>();

        return services;
    }
}
