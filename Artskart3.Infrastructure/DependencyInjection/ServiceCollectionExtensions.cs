using Microsoft.Extensions.DependencyInjection;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Services.Implementations;

namespace Artskart3.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<ISearchRepository, SearchRepository>();
            // Add other repositories here
            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ISearchService, SearchService>();
            // Add other application services here
            return services;
        }
    }
}
