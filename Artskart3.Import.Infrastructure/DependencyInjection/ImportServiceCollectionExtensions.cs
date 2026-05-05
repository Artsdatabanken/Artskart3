using Artskart3.Import.Core.Domain.RepositoryInterfaces;
using Artskart3.Import.Infrastructure.Data;
using Artskart3.Import.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Import.Infrastructure.DependencyInjection;

public static class ImportServiceCollectionExtensions
{
    public static IServiceCollection AddImportInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ArtskartImportDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddScoped<IGbifDatasetDiscoveryRepository, GbifDatasetDiscoveryRepository>();

        return services;
    }
}
