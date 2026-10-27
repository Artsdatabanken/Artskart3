using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Postene som er tolket og venter på å bli skrevet. Ren minnestruktur uten
/// EF-sporing.
///
/// HVORFOR SÅ STORE BUNTER: columnstore-indeksen krever minst 102 400 rader i én
/// setning for å komprimere en radgruppe. Artskart 2 kaller <c>SaveChanges()</c> hver
/// 100. post, og det ville gitt delta-store-rader resten av livet. Terskelen er
/// foreslått til 250 000 i planen (4.4), konfigurerbart.
///
/// LOKALITETER DELES PÅ TVERS AV OBSERVASJONER, og det er grunnen til at de ligger som
/// en egen samling her i stedet for én per observasjon: en bunt på 250 000 poster fra
/// samme node har langt færre distinkte lokaliteter, og hver av dem skal bli én rad.
/// </summary>
public class ImportBatch
{
    private readonly List<ImportedObservation> _observations = [];

    /// <summary>
    /// Lokalitetene i bunten som ikke alt finnes i basen, nøklet på
    /// <see cref="ImportedLocation.LookupId"/>.
    /// </summary>
    private readonly Dictionary<string, ImportedLocation> _locations = new(StringComparer.Ordinal);

    public IReadOnlyList<ImportedObservation> Observations => _observations;

    /// <summary>
    /// Lokalitetene som må skrives. En lokalitet som alt er kjent — fordi
    /// <see cref="LocationIdCache"/> svarte — står ikke her: observasjonen har fått
    /// <c>LocationId</c> direkte.
    /// </summary>
    public IReadOnlyCollection<ImportedLocation> Locations => _locations.Values;

    /// <summary>Antall observasjoner i bunten.</summary>
    public int Count => _observations.Count;

    /// <summary>
    /// Legger posten i bunten.
    ///
    /// LOKALITETEN SLÅS OPP I BUFFERET FØRST. Treffer den, settes
    /// <c>LocationId</c> og lokaliteten trenger ikke skrives igjen. Ellers legges den i
    /// <see cref="Locations"/> — og bare én gang per <c>LookupId</c>, uansett hvor mange
    /// poster i bunten som deler den.
    /// </summary>
    public void Add(ImportedObservation observation, LocationIdCache? locationIds = null)
    {
        ArgumentNullException.ThrowIfNull(observation);

        _observations.Add(observation);

        var location = observation.Location;

        if (location is null || string.IsNullOrEmpty(location.LookupId)) return;

        if (locationIds is not null && locationIds.TryGet(location.LookupId, out var kjentId))
        {
            observation.LocationId = kjentId;

            return;
        }

        // Første observasjon med denne nøkkelen eier lokalitetsraden. De neste peker på
        // den samme, og de kan ha ulike områdelister bare hvis geografitjenesten har
        // svart forskjellig på samme koordinat — noe den ikke gjør.
        _locations.TryAdd(location.LookupId, location);
    }

    public void Clear()
    {
        _observations.Clear();
        _locations.Clear();
    }
}
