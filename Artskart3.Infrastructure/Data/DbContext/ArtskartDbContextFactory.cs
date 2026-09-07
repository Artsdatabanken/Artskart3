using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Infrastructure.Data;

/// <summary>
/// Used by EF Core tooling (dotnet ef migrations add, dotnet ef database update) at design time.
/// Reads the connection string from appsettings.json in the API project.
/// </summary>
public class ArtskartDbContextFactory : IDesignTimeDbContextFactory<ArtskartDbContext>
{
    public ArtskartDbContext CreateDbContext(string[] args)
    {
        // Miljøvariabler leses til slutt slik at de overstyrer json-filene.
        // Nødvendig for byggeagenter: appsettings.json har tom ArtskartIndex, og
        // user secrets finnes bare på utviklermaskiner. Uten dette kan ikke
        // «dotnet ef migrations bundle» kjøre i pipeline.
        // Sett ConnectionStrings__ArtskartIndex for å styre den.
        // BEGGE json-filene er valgfrie, og basisstien må finnes.
        //
        // Denne factoryen brukes ikke bare av «dotnet ef» i repoet — den er også det
        // migrasjonsbundtet (efbundle) bygger sin DbContext med. Bundtet publiseres
        // som en enkelt kjørbar uten appsettings.json, og slett ikke på stien
        // ../Artskart3.Api/. Med optional: false kastet den derfor før den rakk å
        // lese tilkoblingsstrengen, og hele migrasjonsleveransen var ubrukelig ved
        // deploy. Miljøvariabelen under dekker det tilfellet.
        var apiSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Artskart3.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiSettingsPath) ? apiSettingsPath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("8dc47386-a52c-4e4e-8671-c5d5cd04ea81")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ArtskartIndex");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ArtskartIndex' er tom. Sett den i user secrets lokalt, " +
                "eller som miljøvariabelen ConnectionStrings__ArtskartIndex i pipeline.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ArtskartDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.UseNetTopologySuite();

            // 6 timer. Gjelder kun EF-verktøyet og migrasjonsbundtet — aldri
            // applikasjonen, som har sin egen konfigurasjon i Program.cs.
            //
            // Migrasjonen BackfillAll er én enkelt kommando som fyller hele
            // datasettet, og hele løkken teller mot dette ene budsjettet. Den
            // gamle grensen på 1800 s er allerede truffet for ekte, av den
            // opprinnelige datafyllingen av ObservationTaxonHierarchy.
            //
            // 6 timer er satt likt med maksgrensen for en Microsoft-hosted
            // pipeline-jobb (360 min), så det er jobben som setter den reelle
            // grensen — ikke en vilkårlig verdi her.
            sqlOptions.CommandTimeout(21600);
        });

        return new ArtskartDbContext(optionsBuilder.Options);
    }
}
