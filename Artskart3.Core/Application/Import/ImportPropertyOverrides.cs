using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Feltoverstyringene for en node, lest fra <c>DataSource.ImportPropertyOverrides</c>.
/// Port av Artskart 2s <c>PropertyOverrider.ApplyPropertyOverRides</c>.
///
/// HVA DE ER FOR: en kilde som alltid sender samme gale verdi — en tom
/// <c>basisOfRecord</c>, en institusjonskode som mangler — kan rettes i registeret i
/// stedet for i koden. Overstyringen settes på hver post fra noden.
///
/// ANVENDES PÅ SAMME STED SOM I ARTSKART 2: rett etter lesing fra cache, før
/// annotasjoner og før stegkjeden. Rekkefølgen er lastbærende — en overstyring skal
/// kunne overskrives av en annotasjon, ikke omvendt.
///
/// ET UKJENT FELTNAVN KASTER, det ignoreres ikke. Det er Artskart 2s oppførsel, og den
/// er riktig: en skrivefeil i registeret skal stoppe noden, ikke gi en node som ser ut
/// til å gå fint mens overstyringen aldri slår inn. FORSKJELLEN er når det skjer —
/// <see cref="Parse"/> leser og validerer alt én gang før noden starter, mens Artskart
/// 2 oppdager det på første post.
/// </summary>
public sealed class ImportPropertyOverrides
{
    /// <summary>Ingen overstyringer. Koster ingenting å anvende.</summary>
    public static readonly ImportPropertyOverrides None = new([]);

    private readonly List<Overstyring> _overstyringer;

    private ImportPropertyOverrides(List<Overstyring> overstyringer)
    {
        _overstyringer = overstyringer;
    }

    /// <summary>Antall felt som overstyres.</summary>
    public int Count => _overstyringer.Count;

    /// <summary>Feltnavnene som overstyres, i rekkefølgen de anvendes.</summary>
    public IReadOnlyList<string> Fields => _overstyringer.Select(x => x.Felt).ToList();

    /// <summary>
    /// Leser JSON-en fra registeret. Tomt eller blankt gir
    /// <see cref="None"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// JSON-en er ugyldig, et feltnavn finnes ikke på <see cref="CacheRecord"/>, eller
    /// en verdi lar seg ikke lese som feltets type.
    /// </exception>
    public static ImportPropertyOverrides Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return None;

        Dictionary<string, string?> lest;

        try
        {
            lest = ImportValueHelper.ReadJsonAsTextPairs(json, "ImportPropertyOverrides");
        }
        catch (Exception feil)
        {
            throw new InvalidOperationException(
                $"ImportPropertyOverrides er ikke et gyldig JSON-objekt med tekstverdier: {json}", feil);
        }

        var ukjente = lest.Keys.Where(x => !CacheRecordFields.CanSet(x)).ToArray();

        if (ukjente.Length > 0)
        {
            throw new InvalidOperationException(
                "ImportPropertyOverrides viser til felt som ikke finnes på CacheRecord: "
                + string.Join(", ", ukjente));
        }

        var overstyringer = lest
            .Select(x => new Overstyring(x.Key, CacheRecordFields.Setter(x.Key)!, x.Value))
            .ToList();

        // Konverteringen prøves med en gang, mot en kastepost. En verdi som ikke lar
        // seg lese som feltets type skal stoppe noden før den begynner.
        var kastepost = new CacheRecord();

        foreach (var overstyring in overstyringer)
        {
            try
            {
                overstyring.Sett(kastepost, overstyring.Verdi);
            }
            catch (Exception feil)
            {
                throw new InvalidOperationException(
                    $"ImportPropertyOverrides kan ikke sette {overstyring.Felt} til '{overstyring.Verdi}'.", feil);
            }
        }

        return new ImportPropertyOverrides(overstyringer);
    }

    /// <summary>Setter overstyringene på posten.</summary>
    public void ApplyTo(CacheRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        foreach (var overstyring in _overstyringer)
        {
            overstyring.Sett(record, overstyring.Verdi);
        }
    }

    private sealed record Overstyring(string Felt, Action<CacheRecord, string?> Sett, string? Verdi);
}
