namespace Artskart3.Core.Application.Configuration;

public class LocationCountCacheOptions
{
    public const string SectionName = "LocationCountCache";

    /// <summary>
    /// Slår av oppslag mot lokasjonsbufferen uten deploy. Av betyr at alle kall
    /// aggregerer fra indekstabellen som før. Bufferen kan bare gjøre svaret
    /// raskere, aldri nødvendig for at noe skal virke — så dette er en ren
    /// nødbryter dersom den skulle vise seg å gi andre tall enn tellingen.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
