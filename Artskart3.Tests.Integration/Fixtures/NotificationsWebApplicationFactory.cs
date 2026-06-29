using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Artskart3.Tests.Integration.Fixtures;

/// <summary>
/// Lettvekts WebApplicationFactory for endepunkter som ikke bruker databasen.
/// Krever ikke Docker — ingen SQL Server-container startes.
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
    }
}
