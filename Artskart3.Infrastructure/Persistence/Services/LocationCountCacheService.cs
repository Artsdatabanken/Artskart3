using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// Leser ferdig talte lokasjoner fra LocationCountCacheLevel1.
///
/// Tjenesten bygger ikke bufferen — den leser bare. Finnes ingen rad i
/// LocationCountCacheState med Status = 'Ready', returnerer hvert kall null og
/// kalleren aggregerer som før. Det gjør endringen inert til byggejobben er på
/// plass.
///
/// BARE PUNKTLOKASJONER — POLYGONENE HAR SIN EGEN VEI
/// Denne betjener GetLocationsAsync. Polygonsøket hadde en tilsvarende buffer
/// en kort stund, men svarer nå fra PolygonLocationStore, som holder hele
/// datasettet i minnet og derfor ikke er begrenset til ett filter.
///
/// DIMENSJONENE ER DE SAMME SOM OMRÅDEBUFFERENS
/// Bøttingen er identisk — bare grupperingsnøkkelen er en annen: lokasjon i
/// stedet for område. Derfor gjenbrukes AreaCountCacheDimensions i sin helhet,
/// AreaCountCacheBucketMember inkludert. Det er ikke gjenbruk for gjenbrukets
/// skyld: hadde de to bufferne nummerert bøttene hver for seg, ville
/// medlemstabellen måttet dupliseres, og to nummereringer som skal være like er
/// to nummereringer som en dag ikke er det.
///
/// HVORFOR BARE ETT NIVÅ
/// Bufferen lagrer marginaler. To filtre krever samfordelingen, og den lar seg
/// ikke utlede: en observasjon som treffer begge ligger i begge bøttene, så
/// summering overteller og minimum er bare en øvre grense. Det er heller ikke
/// der tiden går — målt over Full-nivået er to-filter-kallene 389 ms i snitt,
/// mens ett-filter- og de ufiltrerte kallene står for 59 % av lokasjonstiden.
/// </summary>
public class LocationCountCacheService : ILocationCountCacheService
{
    /// <summary>
    /// Dimensjonen for «ingen filter». Den finnes ikke i
    /// AreaCountCacheDimensions — områdebufferen spørres aldri uten filter — men
    /// her er det det tregeste kallet vi har målt. Bøtte-id-en er alltid 0.
    /// </summary>
    public const byte NoFilterDimensionId = 0;

    public const int NoFilterBucketId = 0;

    private readonly IArtsKartDbContext _context;
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly IAreaHierarchyService _areaHierarchy;
    private readonly LocationCountCacheOptions _options;
    private readonly ILogger<LocationCountCacheService> _logger;

    public LocationCountCacheService(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        IAreaHierarchyService areaHierarchy,
        IOptions<LocationCountCacheOptions> options,
        ILogger<LocationCountCacheService> logger)
    {
        _context = context;
        _taxonHierarchy = taxonHierarchy;
        _areaHierarchy = areaHierarchy;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Én dimensjon med bøttene ferdig oppløst til id-er eller et intervall.</summary>
    private sealed record ResolvedDimension(byte DimensionId, int[]? Buckets, BucketRange? Range);

    /// <summary>
    /// Tilstanden bufferen ble lest under. Sammenlignes etter lesingen, se
    /// <see cref="BekreftTilstandAsync"/>.
    /// </summary>
    private sealed record CacheTilstand(string Status, int SchemaVersion, long? SourceRows, DateTime? BuiltAt);

    /// <inheritdoc />
    public async Task<List<LocationModel>?> TryGetTopLocationsAsync(
        LocationSearchFilterDto filter,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        const byte stateId = LocationCountCacheState.PointCacheId;

        var (kanSvare, dimensjon, tilstand) = await BestemAsync(filter, stateId, cancellationToken);

        if (!kanSvare) return null;

        // Også det tomme svaret bekreftes: beslutningen over leste medlemstabellen,
        // og en halvveis tømt tabell kan gi «ingen bøtte» for et filter som har treff.
        if (dimensjon is null)
            return await BekreftTilstandAsync(tilstand!, stateId, cancellationToken) ? [] : null;

        var resultat = await LookupAsync(dimensjon, FilterMask.Select(filter), filter.Envelope, maxResults, cancellationToken);

        return await BekreftTilstandAsync(tilstand!, stateId, cancellationToken) ? resultat : null;
    }

    /// <summary>
    /// Bekrefter at bufferen ikke ble bygget om mens den ble lest.
    ///
    /// Ferskhetssjekken skjer før lesingen, og mellom de to er det ingenting som
    /// holder bufferen i ro. BuildLocationCountCache.sql tømmer tabellen, så et
    /// oppslag midt i en gjenoppbygging leser en halvveis tom buffer og svarer med
    /// for få lokasjoner — eller ingen — med HTTP 200 og ingen logglinje.
    ///
    /// Det er nok å lese tilstandsraden på nytt: skriptet setter Status til
    /// «Building» FØR det tømmer noe, så dataene kan ikke endres uten at denne raden
    /// er endret først. Rekker hele byggingen seg ferdig mellom de to lesingene, er
    /// Status «Ready» igjen, men BuiltAt og SourceRows er nye.
    ///
    /// Forkastes svaret, teller kalleren som før: tregere, aldri galt.
    /// </summary>
    private async Task<bool> BekreftTilstandAsync(
        CacheTilstand lest, byte stateId, CancellationToken cancellationToken)
    {
        var naa = await LesTilstandAsync(stateId, cancellationToken);

        if (naa == lest)
            return true;

        _logger.LogWarning(
            "Lokasjonsbuffer {StateId} ble endret mens den ble lest (fra {Lest} til {Naa}). Svaret forkastes, og kallet teller i stedet.",
            stateId, lest, naa);
        return false;
    }

    private async Task<CacheTilstand?> LesTilstandAsync(byte stateId, CancellationToken cancellationToken) =>
        await _context.Set<LocationCountCacheState>()
            .AsNoTracking()
            .Where(s => s.Id == stateId)
            .Select(s => new CacheTilstand(s.Status, s.SchemaVersion, s.SourceRows, s.BuiltAt))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Avgjør om bufferen kan svare på filteret, og hvilken bøtte det peker på.
    ///
    /// Tre utfall, som i ResolveAsync:
    ///   (false, null, null) — kan ikke besvares; kalleren teller som før
    ///   (true,  null, t)    — utvalget traff ingen bøtte: null lokasjoner
    ///   (true,  dim,  t)    — slå opp
    ///
    /// Beslutningen er den risikable delen — en dimensjon som slipper gjennom
    /// uten å være dekket gir for mange lokasjoner med for høye tall — så den
    /// er skilt ut fra oppslaget og står ett sted.
    ///
    /// Tilstanden følger med ut når svaret er ja, fordi den må bekreftes etter at
    /// radene er lest. Se <see cref="BekreftTilstandAsync"/>.
    /// </summary>
    private async Task<(bool KanSvare, ResolvedDimension? Dimensjon, CacheTilstand? Tilstand)> BestemAsync(
        LocationSearchFilterDto filter, byte stateId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return (false, null, null);

        // Katalognummer er ikke en dimensjon og kan ikke bli det: ObservationIds
        // er en vilkårlig liste løst opp av typeahead-endepunktet, uten noe
        // endelig vokabular å bøtte på. Den trenger det heller ikke — filteret er
        // et seek på klyngeindeksen og er den raskeste stien vi har.
        if (filter.ObservationIds?.Length > 0)
            return (false, null, null);

        var dimensionContext = new CacheDimensionContext(filter, _taxonHierarchy, _areaHierarchy);
        var active = new List<(CacheDimension Dimension, BucketSelection Buckets)>();

        foreach (var dimension in AreaCountCacheDimensions.All)
        {
            var selection = dimension.Select(dimensionContext);

            // Dimensjonen er i bruk, men kan ikke besvares herfra — for eksempel
            // et takson under ordensnivå. Hele oppslaget faller gjennom; å svare
            // på resten av filteret og se bort fra denne ville gitt for mange
            // lokasjoner med for høye tall.
            if (!selection.IsCacheable)
                return (false, null, null);

            if (selection.IsActive)
                active.Add((dimension, selection.Buckets!));
        }

        // Bare ett nivå. To eller flere filtre krever samfordelingen, se
        // klassekommentaren.
        if (active.Count > 1)
            return (false, null, null);

        var tilstand = await KlarTilstandAsync(stateId, cancellationToken);
        if (tilstand is null)
            return (false, null, null);

        if (active.Count == 0)
            return (true, new ResolvedDimension(NoFilterDimensionId, [NoFilterBucketId], null), tilstand);

        var (aktiv, buckets) = active[0];
        var one = await ResolveAsync(aktiv, buckets, cancellationToken);

        // Ikke i stand til å svare — medlemstabellen er ikke bygget for denne
        // dimensjonen. Skilt fra «ingen bøtte matchet» med vilje, se ResolveAsync.
        if (one is null)
            return (false, null, null);

        // Et filter som ikke treffer noen bøtte gir ingen observasjoner, altså
        // ingen lokasjoner. Samme utfall som tellingen ville gitt.
        if (one.Buckets is { Length: 0 })
            return (true, null, tilstand);

        return (true, one, tilstand);
    }

    /// <summary>
    /// Bufferen brukes bare når den er ferdig bygget, bygget med samme
    /// dimensjonsregister som koden her kjenner, OG bygget fra de dataene som ligger
    /// der nå. Uten versjonssjekken ville en buffer bygget før en dimensjon ble lagt
    /// til eller endret svart med bøtte-id-er som ikke lenger betyr det oppslaget
    /// tror. Uten radtallsjekken ville en buffer eldre enn dataene blitt trodd —
    /// gamle tall i riktig format, uten en eneste feilmelding.
    ///
    /// Radtallsjekken fanger det som flytter radantallet i ObservationEntityIndex,
    /// ikke en endring som bytter verdier uten å endre antallet. Den er et
    /// sikkerhetsnett under importen, ikke en erstatning for at importen
    /// ugyldiggjør bufferen selv (se ImportJob).
    ///
    /// Returnerer tilstanden den sa ja til, ikke bare ja: den må bekreftes etter at
    /// radene er lest. Se <see cref="BekreftTilstandAsync"/>.
    /// </summary>
    private async Task<CacheTilstand?> KlarTilstandAsync(byte stateId, CancellationToken cancellationToken)
    {
        var state = await LesTilstandAsync(stateId, cancellationToken);

        if (state is null || state.Status != "Ready")
            return null;

        if (state.SchemaVersion != AreaCountCacheDimensions.SchemaVersion)
        {
            _logger.LogWarning(
                "Lokasjonsbuffer {StateId} er bygget med skjemaversjon {Built}, koden forventer {Expected}. Bufferen ignoreres til den er bygget på nytt.",
                stateId, state.SchemaVersion, AreaCountCacheDimensions.SchemaVersion);
            return null;
        }

        return await KildenErUendretAsync(state.SourceRows, stateId, cancellationToken)
            ? state
            : null;
    }

    /// <summary>
    /// Sammenligner radantallet bufferen ble bygget fra med det som ligger der nå.
    ///
    /// Alle usikre utfall gir false. Mangler SourceRows, eller er katalogen
    /// utilgjengelig (<c>VIEW DATABASE STATE</c> mangler), KAN vi ikke vite om
    /// bufferen er fersk — og da er aggregering det trygge svaret. Det koster tid; å
    /// svare fra en buffer vi ikke kan verifisere koster riktighet.
    /// </summary>
    private async Task<bool> KildenErUendretAsync(
        long? builtFromRows, byte stateId, CancellationToken cancellationToken)
    {
        if (builtFromRows is null)
        {
            _logger.LogWarning(
                "Lokasjonsbuffer {StateId} mangler SourceRows og kan ikke verifiseres mot dataene. Faller tilbake til telling.",
                stateId);
            return false;
        }

        long naa;
        try
        {
            naa = await CountCacheSourceRows.ReadAsync(_context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Kunne ikke lese radantallet for ObservationEntityIndex. Lokasjonsbuffer {StateId} brukes ikke.",
                stateId);
            return false;
        }

        if (naa == builtFromRows)
            return true;

        _logger.LogWarning(
            "Lokasjonsbuffer {StateId} er bygget fra {Bygget} rader, ObservationEntityIndex har {Naa}. Bufferen er eldre enn dataene og brukes ikke.",
            stateId, builtFromRows, naa);
        return false;
    }

    /// <summary>
    /// Gjør et utvalg om til bøtte-id-er. Flerverdi-dimensjonene må slå opp
    /// hvilke bøtter som inneholder de valgte medlemmene; de andre er allerede
    /// bøtter.
    ///
    /// TRE UTFALL, IKKE TO
    ///   ikke-tom liste — bøttene som skal summeres
    ///   tom liste      — utvalget traff ingen bøtte: null lokasjoner
    ///   null           — kan ikke besvares: fall tilbake til telling
    ///
    /// Skillet mellom de to siste er hele poenget. Er medlemstabellen ikke bygget
    /// for dimensjonen, treffer ingen medlemmer noen bøtte — og hadde det blitt
    /// lest som «null lokasjoner», ville et fylkesvalg gitt et tomt kart uten noen
    /// feilmelding. Derfor sjekkes det eksplisitt om dimensjonen har medlemmer i
    /// det hele tatt, men bare i det tomme tilfellet, så den vanlige stien ikke
    /// betaler for det.
    /// </summary>
    private async Task<ResolvedDimension?> ResolveAsync(
        CacheDimension dimension, BucketSelection buckets, CancellationToken cancellationToken)
    {
        if (buckets is BucketRange range)
            return new ResolvedDimension(dimension.Id, null, range);

        var values = ((BucketSet)buckets).Values;
        if (values.Count == 0)
            return new ResolvedDimension(dimension.Id, [], null);

        if (dimension.Kind != CacheDimensionKind.MultiValue)
            return new ResolvedDimension(dimension.Id, values.Distinct().ToArray(), null);

        var memberIds = values.ToArray();
        var dimensionId = dimension.Id;

        var members = _context.Set<AreaCountCacheBucketMember>().AsNoTracking();

        var resolved = await members
            .Where(m => m.DimensionId == dimensionId && memberIds.Contains(m.MemberId))
            .Select(m => m.BucketId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (resolved.Length > 0)
            return new ResolvedDimension(dimension.Id, resolved, null);

        var harMedlemmer = await members
            .AnyAsync(m => m.DimensionId == dimensionId, cancellationToken);

        if (harMedlemmer)
            return new ResolvedDimension(dimension.Id, [], null);

        _logger.LogWarning(
            "Lokasjonsbufferen mangler medlemmer for dimensjon {Dimension}. Faller tilbake til telling.",
            dimension.Name);
        return null;
    }

    /// <summary>
    /// De mest observerte lokasjonene i utsnittet.
    ///
    /// Klyngenøkkelen er (DimensionId, BucketId, East, North, LocationId), så
    /// bøtta og utsnittet leses som ett sammenhengende rekkeviddesøk.
    /// East og North ligger i tabellen nettopp for å slippe en join mot Location
    /// FØR sorteringen — det var den joinen som kostet 2332 ms i den
    /// opprinnelige analysen. Latitude og Longitude hentes derfor etterpå, på de
    /// radene som faktisk returneres, akkurat som i tellestien.
    /// </summary>
    private async Task<List<LocationModel>> LookupAsync(
        ResolvedDimension dimension, FilterMask.Selection maske, EnvelopeDto? envelope, int maxResults,
        CancellationToken cancellationToken)
    {
        var dimensionId = dimension.DimensionId;
        var query = _context.Set<LocationCountCacheLevel1>()
            .AsNoTracking()
            .Where(r => r.DimensionId == dimensionId);

        var enBoette = dimension.Buckets is { Length: 1 };

        if (dimension.Buckets is { } bucketIds)
        {
            var eneste = bucketIds[0];
            query = enBoette
                ? query.Where(r => r.BucketId == eneste)
                : query.Where(r => bucketIds.Contains(r.BucketId));
        }
        else
        {
            var range = dimension.Range!;

            // NULL-bøtta utelukkes alltid. En observasjon uten dato eller uten
            // koordinatpresisjon matcher heller ikke en fra/til-sammenligning i SQL.
            query = query.Where(r => r.BucketId != AreaCountCacheDimensions.NullBucket);

            if (range.From is { } from) query = query.Where(r => r.BucketId >= from);
            if (range.To is { } to) query = query.Where(r => r.BucketId <= to);
            if (range.Months is { Count: > 0 } months)
            {
                var monthValues = months.ToArray();
                query = query.Where(r => monthValues.Contains(r.BucketId % 100));
            }
        }

        // Maskepredikatet. Er ingen bool satt, legges ingenting til, og cellens
        // masker summeres av grupperingen under.
        if (maske.IsActive)
        {
            var care = maske.Care;
            var want = maske.Want;
            query = query.Where(r => (r.Mask & care) == want);
        }

        // Samme avkorting til int som ApplyEnvelopeFilterToEntityIndex. Formen må
        // være lik på fortegnet nøyaktig: (int) på et negativt tall runder mot
        // null og ikke nedover, og et utsnitt vest for sonemidten ville ellers
        // fått andre grenser fra bufferen enn fra tellingen.
        if (envelope is not null)
        {
            var minX = (int)envelope.MinX;
            var maxX = (int)envelope.MaxX;
            var minY = (int)envelope.MinY;
            var maxY = (int)envelope.MaxY;

            query = query.Where(r => r.East >= minX && r.East <= maxX
                                     && r.North >= minY && r.North <= maxY);
        }

        // ÉN BØTTE TRENGER INGEN AGGREGERING
        // (DimensionId, BucketId, East, North, LocationId) er unik i tabellen, så
        // med nøyaktig én bøtte står antallet ferdig på raden. Da slipper SQL
        // Server hash-aggregatet og går rett på sorteringen — og ett valgt takson
        // eller én kategori er det aller vanligste filteret.
        var topp = enBoette
            ? query.Select(r => new { r.LocationId, Count = r.ObservationCount })
            : query
                .GroupBy(r => r.LocationId)
                .Select(g => new { LocationId = g.Key, Count = g.Sum(r => r.ObservationCount) });

        return await topp
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.LocationId)
            .Take(maxResults)
            .Join(_context.Set<Location>(),
                a => a.LocationId,
                l => l.Id,
                (a, l) => new LocationModel
                {
                    Id = a.LocationId,
                    Latitude = l.Latitude ?? 0,
                    Longitude = l.Longitude ?? 0,
                    ObservationCount = a.Count
                })
            .ToListAsync(cancellationToken);
    }

}
