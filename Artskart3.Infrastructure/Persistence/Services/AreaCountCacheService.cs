using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// Leser forhåndsberegnede områdeantall fra AreaCountCacheLevel1 og -Level2.
///
/// Tjenesten bygger ikke bufferen — den leser bare. Fins ingen rad i
/// AreaCountCacheState med Status = 'Ready', returnerer hvert kall null og kalleren
/// teller som før. Det gjør endringen inert til byggejobben er på plass.
///
/// HVORFOR SUMMERING ER EKSAKT
/// For enkeltverdi-dimensjoner har hver observasjon nøyaktig én verdi, så å summere
/// over de valgte verdiene teller hver rad én gang. For flerverdi-dimensjonene
/// (verneområde, prosjekt) er bøtta hele mengden observasjonen tilhører — ikke
/// enkeltområdet — så det samme gjelder der. Summerte man i stedet per område, ville
/// de 1 059 410 observasjonene som ligger i mer enn ett verneområde blitt talt flere
/// ganger.
/// </summary>
public class AreaCountCacheService : IAreaCountCacheService
{
    private readonly IArtsKartDbContext _context;
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly IAreaHierarchyService _areaHierarchy;
    private readonly AreaCountCacheOptions _options;
    private readonly ILogger<AreaCountCacheService> _logger;

    public AreaCountCacheService(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        IAreaHierarchyService areaHierarchy,
        IOptions<AreaCountCacheOptions> options,
        ILogger<AreaCountCacheService> logger)
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
    public async Task<Dictionary<(int EntityTypeId, int EntityId), int>?> TryGetCountsAsync(
        LocationSearchFilterDto filter,
        List<Area> areas,
        bool areasAreNarrowed,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return null;

        // Katalognummer er ikke en dimensjon og kan ikke bli det: ObservationIds er en
        // vilkårlig liste løst opp av typeahead-endepunktet, uten noe endelig vokabular
        // å bøtte på. Den trenger det heller ikke — filteret er et seek på
        // klyngeindeksen og er den raskeste stien vi har.
        if (filter.ObservationIds?.Length > 0)
            return null;

        // Annotering og sensitivitet er tagger, og kolonnene finnes bare på
        // Observation — ikke på ObservationEntityIndex som bufferen bygges fra.
        // Uten denne ville bufferen aldri konsultert dem og svart for høyt.
        if (filter.ChangedByAnnotation.HasValue || filter.Sensitive.HasValue)
            return null;

        var dimensionContext = new CacheDimensionContext(filter, _taxonHierarchy, _areaHierarchy);
        var active = new List<(CacheDimension Dimension, BucketSelection Buckets)>();

        foreach (var dimension in AreaCountCacheDimensions.All)
        {
            var selection = dimension.Select(dimensionContext);

            // Dimensjonen er i bruk, men kan ikke besvares herfra — f.eks. et takson
            // under ordensnivå. Hele oppslaget faller gjennom; å svare på resten av
            // filteret og ignorere denne ville gitt for høye tall.
            if (!selection.IsCacheable)
                return null;

            if (selection.IsActive)
                active.Add((dimension, selection.Buckets!));
        }

        // Null dimensjoner havner ikke her — ComputeFilteredAreaCounts kalles bare når
        // HasObservationAttributeFilters er sann. Tre eller flere dekkes ikke: det ville
        // krevd et tredje nivå, og kombinatorikken sprekker.
        if (active.Count is 0 or > 2)
            return null;

        if (active.Count == 2 && !_options.EnableLevel2)
            return null;

        var tilstand = await KlarTilstandAsync(cancellationToken);
        if (tilstand is null)
            return null;

        // Samme cellefilter som ComputeFilteredAreaCounts bruker, med samme forbehold:
        // id-lista sendes bare når den faktisk snevrer inn noe. En uendret områdeliste
        // er alle id-ene for sine typer, og predikatet fjerner da ingen rader — det
        // koster bare én navngitt parameter per id. EF ekspanderer kolleksjonen, og på
        // zoomnivå 2 ble det 400 parametere og 14 kB spørringstekst for å uttrykke «alt».
        var entityTypeIds = areas.Select(a => a.AreaTypeId).Distinct().ToArray();

        int[]? entityIds = null;
        if (areasAreNarrowed)
        {
            entityIds = areas
                .Select(a => _areaHierarchy.FidToEntityId(a.Fid))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToArray();
        }

        // Ingen områder gir ingen celler. Tellingen ville gitt tomt av samme grunn, så
        // tomt er riktig svar — ikke en bom.
        if (entityTypeIds.Length == 0 || entityIds is { Length: 0 })
            return [];

        var resolved = new List<ResolvedDimension>(active.Count);
        foreach (var (dimension, buckets) in active)
        {
            var one = await ResolveAsync(dimension, buckets, cancellationToken);

            // null betyr at dimensjonen ikke kan besvares herfra — medlemstabellen er
            // ikke bygget for den. Hele oppslaget må da falle tilbake til telling.
            // Svarte vi tomt her, ville en ubygget medlemstabell gitt blankt kart.
            if (one is null)
                return null;

            // Tom bøtteliste er noe annet: filteret treffer ingen bøtte, altså ingen
            // observasjoner. Tomt resultat, aldri ufiltrert — samme utfall som
            // spørringen ville gitt, og uten en spørring som garantert gir null rader.
            //
            // Bekreftes likevel: oppslaget over leste medlemstabellen, og en halvveis
            // tømt medlemstabell kan gi «ingen bøtte» for et filter som har treff.
            if (one.Buckets is { Length: 0 })
                return await BekreftTilstandAsync(tilstand, cancellationToken) ? [] : null;

            resolved.Add(one);
        }

        // Dimensjonene kommer i id-rekkefølge fordi AreaCountCacheDimensions.All er
        // deklarert slik. Pargruppe-id-en sorterer også, så BucketA hører alltid til
        // den laveste id-en.
        var resultat = resolved.Count == 1
            ? await LookupLevel1Async(resolved[0], entityTypeIds, entityIds, cancellationToken)
            : await LookupLevel2Async(resolved[0], resolved[1], entityTypeIds, entityIds, cancellationToken);

        return await BekreftTilstandAsync(tilstand, cancellationToken) ? resultat : null;
    }

    /// <summary>
    /// Bekrefter at bufferen ikke ble bygget om mens den ble lest.
    ///
    /// Ferskhetssjekken skjer før lesingen, og mellom de to er det ingenting som
    /// holder bufferen i ro. Byggeskriptene tømmer tabellene, så et oppslag midt i
    /// en gjenoppbygging leser en halvveis tom buffer: resultatet blir for lave tall
    /// eller en tom ordbok, og kalleren filtrerer bort hver celle med
    /// ObservationCount = 0. Utfallet er et BLANKT kart med HTTP 200 — ingen
    /// feilmelding, ingen logglinje, og ikke tilbakefallet til telling som
    /// BuildAreaCountCache.sql lover.
    ///
    /// HVORFOR DET ER NOK Å LESE TILSTANDSRADEN PÅ NYTT
    /// Begge byggeskriptene setter Status til «Building» FØR de tømmer noe — det er
    /// dokumentert i dem som grunnen til rekkefølgen. En gjenoppbygging kan derfor
    /// ikke røre dataene uten å ha endret denne raden først. Fullføres hele
    /// byggingen mellom de to lesingene, er Status «Ready» igjen, men BuiltAt og
    /// SourceRows er nye.
    ///
    /// Forkastes svaret, teller kalleren som før: tregere, aldri galt.
    /// </summary>
    private async Task<bool> BekreftTilstandAsync(CacheTilstand lest, CancellationToken cancellationToken)
    {
        var naa = await LesTilstandAsync(cancellationToken);

        if (naa == lest)
            return true;

        _logger.LogWarning(
            "Områdebufferen ble endret mens den ble lest (fra {Lest} til {Naa}). Svaret forkastes, og kallet teller i stedet.",
            lest, naa);
        return false;
    }

    private async Task<CacheTilstand?> LesTilstandAsync(CancellationToken cancellationToken) =>
        await _context.Set<AreaCountCacheState>()
            .AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => new CacheTilstand(s.Status, s.SchemaVersion, s.SourceRows, s.BuiltAt))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Bufferen brukes bare når den er ferdig bygget, bygget med samme
    /// dimensjonsregister som koden her kjenner, OG bygget fra de dataene som ligger
    /// der nå. Uten versjonssjekken ville en buffer bygget før en ny dimensjon ble
    /// lagt til svart på forskjøvne pargruppe-id-er — altså riktige tall for feil
    /// par. Uten radtallsjekken ville en buffer eldre enn dataene blitt trodd: gamle
    /// tall i riktig format, uten en eneste feilmelding.
    ///
    /// Radtallsjekken fanger det som flytter radantallet i ObservationEntityIndex —
    /// en import, en sletting, en backfill. Den fanger IKKE en endring som bytter
    /// verdier uten å endre antallet. Den er et sikkerhetsnett under importen, ikke
    /// en erstatning for at importen ugyldiggjør bufferen selv (se ImportJob).
    ///
    /// Returnerer tilstanden den sa ja til, ikke bare ja: den må bekreftes etter at
    /// radene er lest. Se <see cref="BekreftTilstandAsync"/>.
    /// </summary>
    private async Task<CacheTilstand?> KlarTilstandAsync(CancellationToken cancellationToken)
    {
        var state = await LesTilstandAsync(cancellationToken);

        if (state is null || state.Status != "Ready")
            return null;

        if (state.SchemaVersion != AreaCountCacheDimensions.SchemaVersion)
        {
            _logger.LogWarning(
                "Områdebufferen er bygget med skjemaversjon {Built}, koden forventer {Expected}. Bufferen ignoreres til den er bygget på nytt.",
                state.SchemaVersion, AreaCountCacheDimensions.SchemaVersion);
            return null;
        }

        return await KildenErUendretAsync(state.SourceRows, "Områdebufferen", cancellationToken)
            ? state
            : null;
    }

    /// <summary>
    /// Sammenligner radantallet bufferen ble bygget fra med det som ligger der nå.
    ///
    /// Alle usikre utfall gir false. Mangler SourceRows, eller er katalogen utilgjengelig
    /// (<c>VIEW DATABASE STATE</c> mangler), KAN vi ikke vite om bufferen er fersk — og
    /// da er aggregering det trygge svaret. Det koster tid; å svare fra en buffer vi ikke
    /// kan verifisere koster riktighet.
    /// </summary>
    private async Task<bool> KildenErUendretAsync(
        long? builtFromRows, string buffernavn, CancellationToken cancellationToken)
    {
        if (builtFromRows is null)
        {
            _logger.LogWarning(
                "{Buffer} mangler SourceRows og kan ikke verifiseres mot dataene. Faller tilbake til telling.",
                buffernavn);
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
                "Kunne ikke lese radantallet for ObservationEntityIndex. {Buffer} brukes ikke.",
                buffernavn);
            return false;
        }

        if (naa == builtFromRows)
            return true;

        _logger.LogWarning(
            "{Buffer} er bygget fra {Bygget} rader, ObservationEntityIndex har {Naa}. Bufferen er eldre enn dataene og brukes ikke.",
            buffernavn, builtFromRows, naa);
        return false;
    }

    /// <summary>
    /// Gjør et utvalg om til bøtte-id-er. Flerverdi-dimensjonene må slå opp hvilke
    /// bøtter som inneholder de valgte medlemmene; de andre er allerede bøtter.
    ///
    /// Tre utfall, som i LocationCountCacheService.ResolveAsync:
    ///
    ///   bøtter        — slå opp disse
    ///   tom liste     — utvalget treffer ingen bøtte: ingen observasjoner
    ///   null          — kan ikke besvares: fall tilbake til telling
    ///
    /// Skillet mellom de to siste er hele poenget. Er medlemstabellen ikke bygget for
    /// dimensjonen, treffer ingen medlemmer noen bøtte — og leses det som «ingen
    /// observasjoner», svarer et verneområdevalg blankt kart uten en eneste
    /// feilmelding. Derfor sjekkes det eksplisitt om dimensjonen har medlemmer i det
    /// hele tatt, men bare i det tomme tilfellet, så den vanlige stien ikke betaler
    /// for det.
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
            return new ResolvedDimension(dimension.Id, values.ToArray(), null);

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
            "Områdebufferen mangler medlemmer for dimensjon {Dimension}. Faller tilbake til telling.",
            dimension.Name);
        return null;
    }

    private async Task<Dictionary<(int EntityTypeId, int EntityId), int>> LookupLevel1Async(
        ResolvedDimension dimension, int[] entityTypeIds, int[]? entityIds, CancellationToken cancellationToken)
    {
        var dimensionId = dimension.DimensionId;
        var query = _context.Set<AreaCountCacheLevel1>()
            .AsNoTracking()
            .Where(r => r.DimensionId == dimensionId);

        if (dimension.Buckets is { } bucketIds)
        {
            query = query.Where(r => bucketIds.Contains(r.BucketId));
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

        query = query.Where(r => entityTypeIds.Contains(r.EntityTypeId));
        if (entityIds is not null)
            query = query.Where(r => entityIds.Contains(r.EntityId));

        var rows = await query
            .GroupBy(r => new { r.EntityTypeId, r.EntityId })
            .Select(g => new { g.Key.EntityTypeId, g.Key.EntityId, Count = g.Sum(r => r.ObservationCount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => (r.EntityTypeId, r.EntityId), r => r.Count);
    }

    private async Task<Dictionary<(int EntityTypeId, int EntityId), int>> LookupLevel2Async(
        ResolvedDimension a, ResolvedDimension b, int[] entityTypeIds, int[]? entityIds, CancellationToken cancellationToken)
    {
        var pairId = AreaCountCacheDimensions.GetPairId(a.DimensionId, b.DimensionId);

        var query = _context.Set<AreaCountCacheLevel2>()
            .AsNoTracking()
            .Where(r => r.DimensionPairId == pairId);

        if (a.Buckets is { } bucketsA)
        {
            query = query.Where(r => bucketsA.Contains(r.BucketA));
        }
        else
        {
            var range = a.Range!;
            query = query.Where(r => r.BucketA != AreaCountCacheDimensions.NullBucket);
            if (range.From is { } from) query = query.Where(r => r.BucketA >= from);
            if (range.To is { } to) query = query.Where(r => r.BucketA <= to);
            if (range.Months is { Count: > 0 } months)
            {
                var monthValues = months.ToArray();
                query = query.Where(r => monthValues.Contains(r.BucketA % 100));
            }
        }

        if (b.Buckets is { } bucketsB)
        {
            query = query.Where(r => bucketsB.Contains(r.BucketB));
        }
        else
        {
            var range = b.Range!;
            query = query.Where(r => r.BucketB != AreaCountCacheDimensions.NullBucket);
            if (range.From is { } from) query = query.Where(r => r.BucketB >= from);
            if (range.To is { } to) query = query.Where(r => r.BucketB <= to);
            if (range.Months is { Count: > 0 } months)
            {
                var monthValues = months.ToArray();
                query = query.Where(r => monthValues.Contains(r.BucketB % 100));
            }
        }

        query = query.Where(r => entityTypeIds.Contains(r.EntityTypeId));
        if (entityIds is not null)
            query = query.Where(r => entityIds.Contains(r.EntityId));

        var rows = await query
            .GroupBy(r => new { r.EntityTypeId, r.EntityId })
            .Select(g => new { g.Key.EntityTypeId, g.Key.EntityId, Count = g.Sum(r => r.ObservationCount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => (r.EntityTypeId, r.EntityId), r => r.Count);
    }
}
