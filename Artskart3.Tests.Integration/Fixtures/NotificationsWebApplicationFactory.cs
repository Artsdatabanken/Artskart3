using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Artskart3.Tests.Integration.Fixtures;

/// <summary>
/// Lettvekts WebApplicationFactory for endepunkter som ikke bruker databasen.
/// Krever ikke Docker — ingen SQL Server-container startes.
/// Erstatter AreaHierarchyService (hosted service) med en stub som ikke kobler til DB.
///
/// Merk: Dersom notifications-lagringen flyttes fra JSON-fil til SQL Server, må denne
/// fabrikkklassen erstattes med <see cref="CustomWebApplicationFactory"/> og testklassen
/// må knyttes til <see cref="DatabaseCollection"/>.
/// </summary>
public sealed class NotificationsWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            // Fjern AreaHierarchyService (hosted service) som prøver å koble til DB ved oppstart
            services.RemoveAll<AreaHierarchyService>();
            services.RemoveAll<IAreaHierarchyService>();
            var areaHostedDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                d.ImplementationFactory?.Method.ReturnType == typeof(AreaHierarchyService));
            if (areaHostedDescriptor != null) services.Remove(areaHostedDescriptor);

            services.AddSingleton<IAreaHierarchyService, StubAreaHierarchyService>();

            // Fjern TaxonHierarchyService (hosted service) som prøver å koble til DB ved oppstart
            services.RemoveAll<TaxonHierarchyService>();
            services.RemoveAll<ITaxonHierarchyService>();
            var taxonHostedDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                d.ImplementationFactory?.Method.ReturnType == typeof(TaxonHierarchyService));
            if (taxonHostedDescriptor != null) services.Remove(taxonHostedDescriptor);

            services.AddSingleton<ITaxonHierarchyService, StubTaxonHierarchyService>();
        });
    }
}
