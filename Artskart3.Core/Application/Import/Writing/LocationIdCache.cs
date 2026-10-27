namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Hvilke <c>Location.LookupId</c> som alt har en rad i basen, og hvilken Id den har.
///
/// HVA DEN SPARER: en lokalitet som er skrevet i en tidligere bunt trenger ikke skrives
/// igjen. Uten bufferet ville hver bunt tatt med alle lokalitetene sine på nytt, og
/// <c>MERGE</c>-en ville gjort samme arbeid om og om igjen — 5,1 millioner lokaliteter
/// fordelt på bunter av 250 000 poster gir mange gjentakelser.
///
/// DEN ER BARE EN OPTIMALISERING. Publiseringen slår opp lokaliteten på
/// <c>LookupId</c> uansett, så et bomskudd her koster arbeid, ikke riktighet. Det er
/// derfor den kan kaste ut oppføringer uten videre.
///
/// ARTSKART 2 TØMMER HELE BUFFERET sitt når det passerer 2 500 oppføringer
/// (<c>ImportGeometry.locationscache</c>). Her kastes den eldste ut i stedet: et buffer
/// som tømmes mister også de nøklene som brukes hele tiden, og for en node som sender
/// poster fra samme håndfull steder er det nettopp dem man vil beholde.
/// </summary>
public class LocationIdCache
{
    /// <summary>
    /// Hvor mange nøkler bufferet holder. 250 000 lokaliteter i minnet er noen titalls
    /// megabyte, og det er langt mer enn én bunt rekker å produsere.
    /// </summary>
    public const int DefaultCapacity = 250_000;

    private readonly int _kapasitet;
    private readonly Dictionary<string, LinkedListNode<Oppføring>> _påNøkkel;

    /// <summary>Eldst først, nyest sist.</summary>
    private readonly LinkedList<Oppføring> _bruksrekkefølge = new();

    public LocationIdCache(int capacity = DefaultCapacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

        _kapasitet = capacity;
        _påNøkkel = new Dictionary<string, LinkedListNode<Oppføring>>(StringComparer.Ordinal);
    }

    public int Count => _påNøkkel.Count;

    public int Capacity => _kapasitet;

    /// <summary>Antall oppslag som har truffet, til logging.</summary>
    public long Hits { get; private set; }

    /// <summary>Antall oppslag som ikke har truffet, til logging.</summary>
    public long Misses { get; private set; }

    public bool TryGet(string lookupId, out int locationId)
    {
        if (_påNøkkel.TryGetValue(lookupId, out var node))
        {
            // Brukt nå, altså nyest.
            _bruksrekkefølge.Remove(node);
            _bruksrekkefølge.AddLast(node);

            locationId = node.Value.LocationId;
            Hits++;

            return true;
        }

        locationId = 0;
        Misses++;

        return false;
    }

    /// <summary>
    /// Fører opp at denne nøkkelen har denne raden. Finnes nøkkelen fra før, oppdateres
    /// Id-en — en lokalitet som har byttet rad er ikke noe vi forventer, men en
    /// oppføring som lyver er verre enn en som er ny.
    /// </summary>
    public void Set(string lookupId, int locationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lookupId);

        if (_påNøkkel.TryGetValue(lookupId, out var eksisterende))
        {
            eksisterende.Value = new Oppføring(lookupId, locationId);

            _bruksrekkefølge.Remove(eksisterende);
            _bruksrekkefølge.AddLast(eksisterende);

            return;
        }

        var node = _bruksrekkefølge.AddLast(new Oppføring(lookupId, locationId));
        _påNøkkel.Add(lookupId, node);

        while (_påNøkkel.Count > _kapasitet)
        {
            var eldst = _bruksrekkefølge.First;

            if (eldst is null) break;

            _bruksrekkefølge.RemoveFirst();
            _påNøkkel.Remove(eldst.Value.LookupId);
        }
    }

    public void Clear()
    {
        _påNøkkel.Clear();
        _bruksrekkefølge.Clear();
    }

    private readonly record struct Oppføring(string LookupId, int LocationId);
}
