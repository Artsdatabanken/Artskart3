using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;

namespace Artskart3.Api.Configuration;

public static class DataProtectionExtensions
{
    /// <summary>
    /// Legger nøkkelringen til Data Protection i blob storage når det er konfigurert.
    /// Uten dette havner den på containerens eget filsystem og forsvinner ved hver
    /// utrulling — da ryker innloggingen for alle, og med mer enn én instans kan de
    /// ikke lese hverandres cookies.
    /// </summary>
    public static IServiceCollection AddConfiguredDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        var options = configuration.GetSection(DataProtectionOptions.SectionName)
            .Get<DataProtectionOptions>() ?? new DataProtectionOptions();

        var builder = services.AddDataProtection()
            .SetApplicationName(options.ApplicationName);

        var blobClient = CreateBlobClient(options, logger);
        if (blobClient is null)
        {
            logger.LogWarning(
                "Data Protection: ingen lagring konfigurert. Nøkkelringen blir liggende " +
                "i containeren og forsvinner når den erstattes. Greit lokalt, men i et " +
                "deployet miljø betyr det at brukerne logges ut ved hver utrulling.");
            return services;
        }

        builder.PersistKeysToAzureBlobStorage(blobClient);
        logger.LogInformation("Data Protection: nøkkelringen lagres i blob storage");

        if (string.IsNullOrWhiteSpace(options.KeyVaultKeyId))
        {
            logger.LogWarning(
                "Data Protection: ingen Key Vault-nøkkel konfigurert. Nøkkelringen " +
                "lagres ukryptert i blob storage.");
        }
        else
        {
            builder.ProtectKeysWithAzureKeyVault(
                new Uri(options.KeyVaultKeyId), new DefaultAzureCredential());
            logger.LogInformation("Data Protection: nøkkelringen krypteres med Key Vault-nøkkel");
        }

        return services;
    }

    private static BlobClient? CreateBlobClient(DataProtectionOptions options, ILogger logger)
    {
        // Deployede miljøer: full URI til blob-en, med managed identity. Containeren
        // oppretter ikke containeren i storage - den må finnes på forhånd.
        if (!string.IsNullOrWhiteSpace(options.BlobUri))
        {
            return new BlobClient(new Uri(options.BlobUri), new DefaultAzureCredential());
        }

        if (string.IsNullOrWhiteSpace(options.BlobConnectionString))
        {
            return null;
        }

        // Tilkoblingsstreng brukes lokalt mot Azurite. Der oppretter vi containeren
        // hvis den mangler, slik at oppsettet virker fra en tom Azurite.
        var container = new BlobContainerClient(options.BlobConnectionString, options.ContainerName);
        try
        {
            container.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Data Protection: kunne ikke opprette blob-containeren {Container}. " +
                "Finnes den allerede, går det fint videre.", options.ContainerName);
        }

        return container.GetBlobClient(options.BlobName);
    }
}
