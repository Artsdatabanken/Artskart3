using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Persistence.Services;
using Artskart3.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IObservationRepository, ObservationRepository>();
        // Add other repositories here
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<AreaHierarchyService>();
        services.AddSingleton<IAreaHierarchyService>(sp => sp.GetRequiredService<AreaHierarchyService>());
        services.AddHostedService(sp => sp.GetRequiredService<AreaHierarchyService>());
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddSingleton<ExportColumnRegistry>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IObservationService, ObservationService>();
        // Add other application services here
        return services;
    }
}
