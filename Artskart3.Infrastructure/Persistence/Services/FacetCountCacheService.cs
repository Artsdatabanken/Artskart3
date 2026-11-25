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
/// Leser forhåndsberegnede fasett-antall fra FacetCountCacheLevel0, -Level1 og -Level2.
///
/// Tjenesten bygger ikke bufferen — den leser bare. Fins ingen rad i
/// FacetCountCacheState med Status = 'Ready', returnerer hvert kall null og kalleren
/// aggregerer som før. Det gjør endringen inert til byggejobben har kjørt.
///
/// HVORFOR SUMMERING ER EKSAKT
/// Bufferen er bygget fra én rad per observasjon (se BuildFacetCountCache.sql), så
/// hver observasjon ligger i nøyaktig én bøtte per dimensjon. Å summere over de
/// valgte bøttene teller derfor hver observasjon én gang. For flerverdi-dimensjonene
/// (verneområde, prosjekt) er bøtta hele mengden observasjonen tilhører — ikke
/// enkeltområdet — så det samme gjelder der.
///
/// MERK: oppløsningen av bøtteutvalg er en tredje kopi av samme logikk, etter
/// AreaCountCacheService.ResolveAsync og LocationCountCacheService.ResolveAsync. Verdt
/// å samle i én delt hjelper, men ikke gjort her for å holde fasett-arbeidet samlet.
/// </summary>
public class FacetCountCacheService : IFacetCountCacheService
{
    private readonly IArtsKartDbContext _context;
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly IAreaHierarchyService _areaHierarchy;
    private readonly FacetCountCacheOptions _options;
    private readonly ILogger<FacetCountCacheService> _logger;

    public FacetCountCacheService(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        IAreaHierarchyService areaHierarchy,
        IOptions<FacetCountCacheOptions> options,
        ILogger<FacetCountCacheService> logger)
    {
        _context = context;
        _taxonHierarchy = taxonHierarchy;
        _areaHierarchy = areaHierarchy;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Ett bøtteutvalg ferdig oppløst til id-er eller et intervall.</summary>
    private sealed record ResolvedConstraint(int[]? Buckets, BucketRange? Range);

    /// <summary>
    /// Tilstanden bufferen ble lest under. Sammenlignes etter lesingen, se
    /// <see cref="BekreftTilstandAsync"/>.
    /// </summary>
    private sealed record CacheTilstand(string Status, int SchemaVersion, long? SourceRows, DateTime? BuiltAt);

    /// <inheritdoc />
    public async Task<Dictionary<int, int>?> TryGetDimensionCountsAsync(
        FacetDimension facet, IObservationFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return null;

        var lookup = FacetCountCacheDimensions.ResolveDimensionFacet(facet, Kontekst(filter));

        // En dimensjonsfasett grupperes på sin egen bøtte, så den bruker opp den ene
        // siden av paret. Nivå 0 finnes ikke for den, og tre eller flere dimensjoner
        // ville krevd et tredje bøttenivå.
        if (lookup.Level is FacetLookupLevel.NotCovered or FacetLookupLevel.Level0)
            return null;

        if (lookup.Level == FacetLookupLevel.Level2 && !_options.EnableLevel2)
            return null;

        var tilstand = await KlarTilstandAsync(cancellationToken);
        if (tilstand is null)
            return null;

        if (lookup.Level == FacetLookupLevel.Level1)
        {
            var nivaa1 = await Level1PerBucketAsync(facet.DimensionId, lookup.Mask, cancellationToken);
            return await BekreftTilstandAsync(tilstand, cancellationToken) ? nivaa1 : null;
        }

        var annen = lookup.Constraints[0];
        var resolved = await ResolveAsync(annen, cancellationToken);

        // null: dimensjonen kan ikke besvares herfra — medlemstabellen er ikke bygget
        // for den. Svarte vi tomt, ville en ubygget medlemstabell gitt null ved hvert
        // filtervalg.
        if (resolved is null)
            return null;

        // Tom bøtteliste er noe annet: filteret treffer ingen bøtte, altså ingen
        // observasjoner. Hvert alternativ er da null — men tilstanden bekreftes, siden
        // en halvveis tømt medlemstabell også gir «ingen bøtte».
        if (resolved.Buckets is { Length: 0 })
            return await BekreftTilstandAsync(tilstand, cancellationToken) ? [] : null;

        var nivaa2 = await Level2PerBucketAsync(
            lookup.PairId!.Value, lookup.GroupByIsBucketA, resolved, lookup.Mask, cancellationToken);

        return await BekreftTilstandAsync(tilstand, cancellationToken) ? nivaa2 : null;
    }

    /// <inheritdoc />
    public async Task<int?> TryGetMaskCountAsync(
        FacetMaskOption facet, bool value, IObservationFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return null;

        var lookup = FacetCountCacheDimensions.ResolveMaskFacet(facet, value, Kontekst(filter));

        if (lookup.Level == FacetLookupLevel.NotCovered)
            return null;

        if (lookup.Level == FacetLookupLevel.Level2 && !_options.EnableLevel2)
            return null;

        var tilstand = await KlarTilstandAsync(cancellationToken);
        if (tilstand is null)
            return null;

        // Bøtteutvalgene løses opp før oppslaget. En tom liste betyr at filteret ikke
        // treffer noen bøtte — altså null observasjoner — mens null betyr at
        // dimensjonen ikke kan besvares herfra.
        var resolvedAll = new List<ResolvedConstraint>(lookup.Constraints.Count);
        foreach (var constraint in lookup.Constraints)
        {
            var resolved = await ResolveAsync(constraint, cancellationToken);
            if (resolved is null)
                return null;

            if (resolved.Buckets is { Length: 0 })
                return await BekreftTilstandAsync(tilstand, cancellationToken) ? 0 : null;

            resolvedAll.Add(resolved);
        }

        var sum = lookup.Level switch
        {
            FacetLookupLevel.Level0 => await Level0SumAsync(lookup.Mask, cancellationToken),
            FacetLookupLevel.Level1 => await Level1SumAsync(
                lookup.Constraints[0].Dimension.Id, resolvedAll[0], lookup.Mask, cancellationToken),
            _ => await Level2SumAsync(
                lookup.PairId!.Value, resolvedAll[0], resolvedAll[1], lookup.Mask, cancellationToken),
        };

        return await BekreftTilstandAsync(tilstand, cancellationToken) ? sum : null;
    }

    private CacheDimensionContext Kontekst(IObservationFilter filter) =>
        new(filter, _taxonHierarchy, _areaHierarchy);

    // -----------------------------------------------------------------------
    // Oppslagene
    // -----------------------------------------------------------------------

    /// <summary>Nivå 1 gruppert på bøtte — fasett-tallene for tomt filter.</summary>
    private async Task<Dictionary<int, int>> Level1PerBucketAsync(
        byte dimensionId, FilterMask.Selection maske, CancellationToken cancellationToken)
    {
        var query = _context.Set<FacetCountCacheLevel1>()
            .AsNoTracking()
            .Where(r => r.DimensionId == dimensionId);

        query = FiltrerPaaMaske(query, maske);

        var rows = await query
            .GroupBy(r => r.BucketId)
            .Select(g => new { BucketId = g.Key, Count = g.Sum(r => r.ObservationCount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.BucketId, r => r.Count);
    }

    /// <summary>
    /// Nivå 2 gruppert på fasettens side av paret, begrenset av motpartens bøtter.
    /// </summary>
    private async Task<Dictionary<int, int>> Level2PerBucketAsync(
        byte pairId, bool facetIsBucketA, ResolvedConstraint annen, FilterMask.Selection maske,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<FacetCountCacheLevel2>()
            .AsNoTracking()
            .Where(r => r.DimensionPairId == pairId);

        // Motparten ligger på den siden fasetten ikke har.
        query = facetIsBucketA
            ? BegrensBucketB(query, annen)
            : BegrensBucketA(query, annen);

        query = FiltrerPaaMaske(query, maske);

        var rows = facetIsBucketA
            ? await query
                .GroupBy(r => r.BucketA)
                .Select(g => new { BucketId = g.Key, Count = g.Sum(r => r.ObservationCount) })
                .ToListAsync(cancellationToken)
            : await query
                .GroupBy(r => r.BucketB)
                .Select(g => new { BucketId = g.Key, Count = g.Sum(r => r.ObservationCount) })
                .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.BucketId, r => r.Count);
    }

    /// <summary>Totalen for én maske, uten noen aktiv dimensjon.</summary>
    private async Task<int> Level0SumAsync(FilterMask.Selection maske, CancellationToken cancellationToken)
    {
        var query = FiltrerPaaMaske(
            _context.Set<FacetCountCacheLevel0>().AsNoTracking(), maske);

        // Nullable sum og ?? 0: et tomt utvalg gir SUM() = NULL i SQL. EF oversetter
        // int-varianten til COALESCE, men det er en detalj i oversetteren — her står
        // det eksplisitt, så et tomt svar er 0 og ikke en kjøretidsoverraskelse.
        return await query.SumAsync(r => (int?)r.ObservationCount, cancellationToken) ?? 0;
    }

    /// <summary>Totalen for én maske, summert over ett filters bøtter.</summary>
    private async Task<int> Level1SumAsync(
        byte dimensionId, ResolvedConstraint constraint, FilterMask.Selection maske,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<FacetCountCacheLevel1>()
            .AsNoTracking()
            .Where(r => r.DimensionId == dimensionId);

        query = BegrensBucket(query, constraint);
        query = FiltrerPaaMaske(query, maske);

        // Nullable sum og ?? 0: et tomt utvalg gir SUM() = NULL i SQL. EF oversetter
        // int-varianten til COALESCE, men det er en detalj i oversetteren — her står
        // det eksplisitt, så et tomt svar er 0 og ikke en kjøretidsoverraskelse.
        return await query.SumAsync(r => (int?)r.ObservationCount, cancellationToken) ?? 0;
    }

    /// <summary>Totalen for én maske, summert over to filtres bøtter.</summary>
    private async Task<int> Level2SumAsync(
        byte pairId, ResolvedConstraint a, ResolvedConstraint b, FilterMask.Selection maske,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<FacetCountCacheLevel2>()
            .AsNoTracking()
            .Where(r => r.DimensionPairId == pairId);

        query = BegrensBucketA(query, a);
        query = BegrensBucketB(query, b);
        query = FiltrerPaaMaske(query, maske);

        // Nullable sum og ?? 0: et tomt utvalg gir SUM() = NULL i SQL. EF oversetter
        // int-varianten til COALESCE, men det er en detalj i oversetteren — her står
        // det eksplisitt, så et tomt svar er 0 og ikke en kjøretidsoverraskelse.
        return await query.SumAsync(r => (int?)r.ObservationCount, cancellationToken) ?? 0;
    }

    // -----------------------------------------------------------------------
    // Predikatene
    // -----------------------------------------------------------------------

    /// <summary>
    /// Maskepredikatet. Er ingen bool satt, legges ingenting til, og bøttas masker
    /// summeres av GroupBy-en eller Sum-en som følger.
    /// </summary>
    private static IQueryable<T> FiltrerPaaMaske<T>(IQueryable<T> query, FilterMask.Selection maske)
        where T : class, IHarFilterMaske
    {
        if (!maske.IsActive) return query;

        var care = maske.Care;
        var want = maske.Want;
        return query.Where(r => (r.Mask & care) == want);
    }

    private static IQueryable<FacetCountCacheLevel1> BegrensBucket(
        IQueryable<FacetCountCacheLevel1> query, ResolvedConstraint constraint)
    {
        if (constraint.Buckets is { } bucketIds)
            return query.Where(r => bucketIds.Contains(r.BucketId));

        var range = constraint.Range!;

        // NULL-bøtta er aldri satt inn av byggeskriptet, så dette predikatet fjerner
        // ingen rader. Det står her fordi områdebufferen har det, og fordi en
        // fremtidig «uten verdi»-bøtte ellers ville sneket seg inn i et intervall.
        query = query.Where(r => r.BucketId != AreaCountCacheDimensions.NullBucket);

        if (range.From is { } from) query = query.Where(r => r.BucketId >= from);
        if (range.To is { } to) query = query.Where(r => r.BucketId <= to);
        if (range.Months is { Count: > 0 } months)
        {
            var monthValues = months.ToArray();
            query = query.Where(r => monthValues.Contains(r.BucketId % 100));
        }

        return query;
    }

    private static IQueryable<FacetCountCacheLevel2> BegrensBucketA(
        IQueryable<FacetCountCacheLevel2> query, ResolvedConstraint constraint)
    {
        if (constraint.Buckets is { } bucketIds)
            return query.Where(r => bucketIds.Contains(r.BucketA));

        var range = constraint.Range!;
        query = query.Where(r => r.BucketA != AreaCountCacheDimensions.NullBucket);
        if (range.From is { } from) query = query.Where(r => r.BucketA >= from);
        if (range.To is { } to) query = query.Where(r => r.BucketA <= to);
        if (range.Months is { Count: > 0 } months)
        {
            var monthValues = months.ToArray();
            query = query.Where(r => monthValues.Contains(r.BucketA % 100));
        }

        return query;
    }

    private static IQueryable<FacetCountCacheLevel2> BegrensBucketB(
        IQueryable<FacetCountCacheLevel2> query, ResolvedConstraint constraint)
    {
        if (constraint.Buckets is { } bucketIds)
            return query.Where(r => bucketIds.Contains(r.BucketB));

        var range = constraint.Range!;
        query = query.Where(r => r.BucketB != AreaCountCacheDimensions.NullBucket);
        if (range.From is { } from) query = query.Where(r => r.BucketB >= from);
        if (range.To is { } to) query = query.Where(r => r.BucketB <= to);
        if (range.Months is { Count: > 0 } months)
        {
            var monthValues = months.ToArray();
            query = query.Where(r => monthValues.Contains(r.BucketB % 100));
        }

        return query;
    }

    // -----------------------------------------------------------------------
    // Oppløsning av bøtteutvalg
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gjør et utvalg om til bøtte-id-er. Flerverdi-dimensjonene må slå opp hvilke
    /// bøtter som inneholder de valgte medlemmene; de andre er allerede bøtter.
    ///
    /// Tre utfall, som i AreaCountCacheService.ResolveAsync:
    ///
    ///   bøtter        — slå opp disse
    ///   tom liste     — utvalget treffer ingen bøtte: ingen observasjoner
    ///   null          — kan ikke besvares: fall tilbake til aggregering
    ///
    /// Skillet mellom de to siste er hele poenget. Er medlemstabellen ikke bygget for
    /// dimensjonen, treffer ingen medlemmer noen bøtte — og leses det som «ingen
    /// observasjoner», viser hvert filtervalg 0 uten en eneste feilmelding.
    ///
    /// MEDLEMSTABELLEN ER OMRÅDEBUFFERENS
    /// Bøtte-nummereringen er en egenskap ved dimensjonen, ikke ved utdatacellen, så
    /// begge bufferne leser AreaCountCacheBucketMember. Byggeskriptet leser den samme
    /// tabellen i stedet for å nummerere på nytt, nettopp for at de to skal stemme.
    /// </summary>
    private async Task<ResolvedConstraint?> ResolveAsync(
        FacetConstraint constraint, CancellationToken cancellationToken)
    {
        if (constraint.Buckets is BucketRange range)
            return new ResolvedConstraint(null, range);

        var values = ((BucketSet)constraint.Buckets).Values;
        if (values.Count == 0)
            return new ResolvedConstraint([], null);

        if (constraint.Dimension.Kind != CacheDimensionKind.MultiValue)
            return new ResolvedConstraint(values.ToArray(), null);

        var memberIds = values.ToArray();
        var dimensionId = constraint.Dimension.Id;

        var members = _context.Set<AreaCountCacheBucketMember>().AsNoTracking();

        var resolved = await members
            .Where(m => m.DimensionId == dimensionId && memberIds.Contains(m.MemberId))
            .Select(m => m.BucketId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (resolved.Length > 0)
            return new ResolvedConstraint(resolved, null);

        var harMedlemmer = await members
            .AnyAsync(m => m.DimensionId == dimensionId, cancellationToken);

        if (harMedlemmer)
            return new ResolvedConstraint([], null);

        _logger.LogWarning(
            "Medlemstabellen mangler rader for dimensjon {Dimension}. Fasett-tallene aggregeres i stedet.",
            constraint.Dimension.Name);
        return null;
    }

    // -----------------------------------------------------------------------
    // Tilstand
    // -----------------------------------------------------------------------

    /// <summary>
    /// Bufferen brukes bare når den er ferdig bygget, bygget med samme register som
    /// koden her kjenner, OG bygget fra de dataene som ligger der nå. Se
    /// AreaCountCacheService for hvorfor alle tre leddene må være med.
    /// </summary>
    private async Task<CacheTilstand?> KlarTilstandAsync(CancellationToken cancellationToken)
    {
        var state = await LesTilstandAsync(cancellationToken);

        if (state is null || state.Status != "Ready")
            return null;

        if (state.SchemaVersion != FacetCountCacheDimensions.SchemaVersion)
        {
            _logger.LogWarning(
                "Fasett-bufferen er bygget med skjemaversjon {Built}, koden forventer {Expected}. Bufferen ignoreres til den er bygget på nytt.",
                state.SchemaVersion, FacetCountCacheDimensions.SchemaVersion);
            return null;
        }

        return await KildenErUendretAsync(state.SourceRows, cancellationToken) ? state : null;
    }

    /// <summary>
    /// Bekrefter at bufferen ikke ble bygget om mens den ble lest.
    ///
    /// Byggeskriptet setter Status til «Building» FØR det tømmer noe, så en
    /// gjenoppbygging kan ikke røre dataene uten å ha endret tilstandsraden først. Et
    /// oppslag midt i en gjenoppbygging ville ellers lest en halvveis tom buffer og
    /// vist 0 ved hvert filtervalg, med HTTP 200 og ingen logglinje.
    /// </summary>
    private async Task<bool> BekreftTilstandAsync(CacheTilstand lest, CancellationToken cancellationToken)
    {
        var naa = await LesTilstandAsync(cancellationToken);

        if (naa == lest)
            return true;

        _logger.LogWarning(
            "Fasett-bufferen ble endret mens den ble lest (fra {Lest} til {Naa}). Svaret forkastes, og kallet aggregerer i stedet.",
            lest, naa);
        return false;
    }

    private async Task<CacheTilstand?> LesTilstandAsync(CancellationToken cancellationToken) =>
        await _context.Set<FacetCountCacheState>()
            .AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => new CacheTilstand(s.Status, s.SchemaVersion, s.SourceRows, s.BuiltAt))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Sammenligner radantallet bufferen ble bygget fra med det som ligger der nå.
    /// Alle usikre utfall gir false: kan vi ikke verifisere at bufferen er fersk, er
    /// aggregering det trygge svaret.
    /// </summary>
    private async Task<bool> KildenErUendretAsync(long? builtFromRows, CancellationToken cancellationToken)
    {
        if (builtFromRows is null)
        {
            _logger.LogWarning(
                "Fasett-bufferen mangler SourceRows og kan ikke verifiseres mot dataene. Faller tilbake til aggregering.");
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
                "Kunne ikke lese radantallet for ObservationEntityIndex. Fasett-bufferen brukes ikke.");
            return false;
        }

        if (naa == builtFromRows)
            return true;

        _logger.LogWarning(
            "Fasett-bufferen er bygget fra {Bygget} rader, ObservationEntityIndex har {Naa}. Bufferen er eldre enn dataene og brukes ikke.",
            builtFromRows, naa);
        return false;
    }
}
