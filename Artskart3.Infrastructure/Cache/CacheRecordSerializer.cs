using System.Text.Json;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Infrastructure.Cache;

/// <summary>
/// JSON-laget mellom en utpakket cache-blob og <see cref="CacheRecord"/>.
///
/// BLOBENE ER SKREVET MED NEWTONSOFT, LESES MED System.Text.Json. Det går fordi
/// Artskart 2 serialiserer med standardinnstillinger pluss
/// <c>DefaultValueHandling.Ignore</c> og <c>NullValueHandling.Ignore</c>: medlemsnavn
/// som de står (PascalCase), ISO 8601-datoer, tall uten anførselstegn, invariant
/// kultur. Det er lesbart for begge bibliotekene. Utelatte felt er nettopp det
/// innstillingene gir — null og standardverdier står ikke i JSON-en — og det er
/// uproblematisk ved lesing.
///
/// HVIS EN EKTE BLOB LIKEVEL IKKE GÅR INN: bytt ut denne ene klassen med Newtonsoft,
/// ikke feltene i <see cref="CacheRecord"/>. Det er derfor JSON-laget ligger alene her
/// og ikke inne i leseren.
///
/// <c>PropertyNameCaseInsensitive</c> er på fordi blobene er skrevet over nesten tjue
/// år; et felt som en gang het noe med annen forbokstav skal fortsatt treffe.
/// </summary>
public static class CacheRecordSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // Harvest har aldri skrevet kommentarer eller etterfølgende komma, men en
        // feilmelding fra JSON-parseren er en avvist post — vi er tolerante der det
        // er gratis.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Kaster <see cref="JsonException"/> på ugyldig JSON. Kallstedet avgjør om det
    /// betyr én avvist post eller en avbrutt kjøring — her kastes det bare videre.
    /// </summary>
    public static CacheRecord Deserialize(string json)
    {
        return JsonSerializer.Deserialize<CacheRecord>(json, Options)
               ?? throw new JsonException("Blobben deserialiserte til null.");
    }
}
