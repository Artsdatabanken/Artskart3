namespace Artskart3.Api.Configuration;

/// <summary>
/// Hvor nøkkelringen til Data Protection lagres. Den krypterer blant annet
/// innloggingscookiene, så uten varig lagring blir brukerne logget ut ved hver
/// utrulling, og med flere instanser kan de ikke lese hverandres cookies.
/// </summary>
public class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Navnet som skiller nøkkelringen fra andre apper som deler lagringen.
    /// Endres dette, blir eksisterende cookies ugyldige.
    /// </summary>
    public string ApplicationName { get; set; } = "Artskart3";

    /// <summary>
    /// Full URI til blob-en nøkkelringen skrives til, inkludert container og filnavn:
    /// https://konto.blob.core.windows.net/container/fil.xml. Brukes med managed
    /// identity. Dette er varianten deployede miljøer skal bruke.
    /// </summary>
    public string? BlobUri { get; set; }

    /// <summary>
    /// Alternativ til <see cref="BlobUri"/> for miljøer uten managed identity, for
    /// eksempel Azurite lokalt. Brukes sammen med <see cref="ContainerName"/> og
    /// <see cref="BlobName"/>.
    /// </summary>
    public string? BlobConnectionString { get; set; }

    public string ContainerName { get; set; } = "dataprotection";

    public string BlobName { get; set; } = "artskart3-keys.xml";

    /// <summary>
    /// Nøkkel i Key Vault som krypterer nøkkelringen i ro, for eksempel
    /// https://hvelv.vault.azure.net/keys/dataprotection. Er den tom, lagres
    /// nøkkelringen ukryptert, og appen logger en advarsel om det.
    /// </summary>
    public string? KeyVaultKeyId { get; set; }
}
