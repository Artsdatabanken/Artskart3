namespace Artskart3.Core.Application.Services;

/// <summary>Hvilket bøttenivå et fasettoppslag skal leses fra.</summary>
public enum FacetLookupLevel
{
    /// <summary>Filteret kan ikke besvares fra bufferen. Kalleren må aggregere.</summary>
    NotCovered,

    /// <summary>Ett tall per maske. Bare for maskefasetter uten andre aktive dimensjoner.</summary>
    Level0,

    /// <summary>Ett tall per (dimensjon, bøtte, maske).</summary>
    Level1,

    /// <summary>Ett tall per (pargruppe, bøtteA, bøtteB, maske).</summary>
    Level2,
}

/// <summary>
/// En annen dimensjon som skal begrense oppslaget, med bøtteutvalget filteret peker ut.
///
/// For flerverdi-dimensjonene (geografi, prosjekt) er <see cref="Buckets"/> medlems-id-er,
/// ikke bøtte-id-er — de må slås opp i medlemstabellen før de kan brukes, som i
/// <c>AreaCountCacheService</c>.
/// </summary>
public sealed record FacetConstraint(CacheDimension Dimension, BucketSelection Buckets);

/// <summary>
/// Formen på ett fasettoppslag: hvilket nivå, hva det skal grupperes på, og hva som
/// begrenser det.
///
/// <see cref="GroupByDimensionId"/> er satt når fasetten er en dimensjon (kategori,
/// taksongruppe, …) og tallene skal komme ut per bøtte. Den er null når fasetten er en
/// bool-bit; da er oppslaget én sum, og bool-valget ligger i <see cref="Mask"/>.
///
/// <see cref="Constraints"/> er sortert på dimensjons-id. Grupperes det ikke, hører den
/// første til <c>BucketA</c> og den andre til <c>BucketB</c>; grupperes det, sier
/// <see cref="GroupByIsBucketA"/> hvilken side fasetten ligger på.
/// </summary>
public sealed record FacetLookup
{
    public required FacetLookupLevel Level { get; init; }

    public byte? GroupByDimensionId { get; init; }

    public IReadOnlyList<FacetConstraint> Constraints { get; init; } = [];

    public FilterMask.Selection Mask { get; init; }

    /// <summary>Pargruppe-id-en, satt kun på <see cref="FacetLookupLevel.Level2"/>.</summary>
    public byte? PairId { get; init; }

    /// <summary>
    /// På Level2 med gruppering: om fasettdimensjonen har lavest id i paret og dermed
    /// ligger i <c>BucketA</c>.
    /// </summary>
    public bool GroupByIsBucketA { get; init; }

    public static FacetLookup NotCovered { get; } = new() { Level = FacetLookupLevel.NotCovered };
}

/// <summary>
/// Dimensjons-id-ene som kan opptre som fasett, navngitt.
///
/// Verdiene er <see cref="AreaCountCacheDimensions"/> sine og ligger i bufferdataene.
/// De står her bare for at kode som må nevne én bestemt dimensjon — som
/// <c>ApplyAttributeFiltersToEntityIndex</c>, der fasettens egen dimensjon skal
/// utelates — slipper å skrive et tall. <see cref="FacetCountCacheDimensions.All"/>
/// slår dem opp i registeret ved oppstart, så et tall som ikke finnes der stopper
/// applikasjonen framfor å bli stående som en stille bom.
/// </summary>
public static class FacetDimensionIds
{
    public const byte RegistrationStatus = 2;
    public const byte Behavior = 3;
    public const byte BasisOfRecord = 4;
    public const byte Category = 5;
    public const byte Institution = 6;
    public const byte TaxonGroup = 7;
}

/// <summary>
/// Én fasett som er en dimensjon. <paramref name="Key"/> er navnet API-et og frontend
/// bruker; det er en del av kontrakten og endres ikke uten at klienten endres i takt.
/// </summary>
public sealed record FacetDimension(string Key, CacheDimension Dimension)
{
    public byte DimensionId => Dimension.Id;

    /// <summary>
    /// Kolonnen det grupperes på ved live aggregering. Hentet fra dimensjonsregisteret,
    /// ikke skrevet opp på nytt — to definisjoner av samme bøtte er den feilen som gir
    /// riktige tall for feil filter.
    /// </summary>
    public string GroupByColumn => Dimension.SqlExpression;
}

/// <summary>
/// Én fasett som er en bool-bit i <see cref="FilterMask"/>. Tre alternativer i UI
/// («alle», ja, nei) blir to oppslag: biten satt og biten klarert.
/// </summary>
public sealed record FacetMaskOption(string Key, byte Bit);

/// <summary>
/// Hvilke dimensjoner som kan vises som fasett i filterpanelet, og hvordan et
/// fasettoppslag mot fasett-bufferen formes.
///
/// EN FASETT-TELLING ER EN OMRÅDETELLING UTEN OMRÅDE
/// Områdebufferen svarer «antall per område, gitt filteret». Fasett-bufferen svarer
/// «antall per bøtte i én dimensjon, gitt filteret uten den dimensjonen». Det er samme
/// bøtter, samme maske og samme pargrupper — bare uten utdatacellen. Dette registeret
/// legger derfor ingen nye dimensjoner til: det peker på
/// <see cref="AreaCountCacheDimensions"/> og gjenbruker id-ene og pargruppe-id-ene
/// derfra. Et eget nummereringsskjema her ville vært en ny kilde til at bufferen svarer
/// riktige tall for feil filter.
///
/// EGEN DIMENSJON TELLER IKKE
/// Tallet ved et valg svarer på «hva får du hvis du velger denne», ikke «hva har du nå».
/// Brukerens valg i samme gruppe utelates derfor når dekningen regnes ut — ellers ville
/// alle ikke-valgte alternativer blitt null i det brukeren krysset av det første.
///
/// DEKNING
/// Antallet ANDRE aktive dimensjoner avgjør nivået. S er de aktive dimensjonene etter at
/// fasettens egen er fjernet:
///
///   fasett        |S|=0    |S|=1               |S|=2               |S|>=3
///   dimensjon     Level1   Level2              ikke dekket         ikke dekket
///   bool-bit      Level0   Level1 (summert)    Level2 (summert)    ikke dekket
///
/// Maskefasettene når ett hakk lenger fordi de ikke bruker opp en bøttedimensjon selv.
/// Det som ikke dekkes, faller til aggregering — målt 130-250 ms ved to eller flere
/// filtre, mot 3,5-7,5 s for de brede filtrene bufferen tar.
///
/// HVORFOR BARE SEKS DIMENSJONER
/// Fasettene må kunne vises som en liste med et tall per rad. Periode og
/// koordinatpresisjon er intervaller, ikke lister. Geografi som fasett er antall per
/// område, altså det <c>GetAreaCounts</c> alt svarer på. Takson hører til artstreet og er
/// et mye større problem. Datasett, prosjekt og katalognummer velges i typeahead, ikke
/// fra en liste. Derfor står bare de seks enkeltverdi-dimensjonene her.
/// </summary>
public static class FacetCountCacheDimensions
{
    /// <summary>
    /// Dimensjonsfasettene, i samme rekkefølge som filterpanelet viser dem.
    ///
    /// Id-ene kommer fra <see cref="AreaCountCacheDimensions"/> og slås opp ved
    /// oppstart, slik at en omnummerering der ikke kan gå upåaktet hen her.
    /// </summary>
    public static readonly IReadOnlyList<FacetDimension> All =
    [
        Facet("taxonGroup", FacetDimensionIds.TaxonGroup),
        Facet("category", FacetDimensionIds.Category),
        Facet("behavior", FacetDimensionIds.Behavior),
        Facet("basisOfRecord", FacetDimensionIds.BasisOfRecord),
        Facet("institution", FacetDimensionIds.Institution),
        Facet("registrationStatus", FacetDimensionIds.RegistrationStatus),
    ];

    /// <summary>
    /// Maskefasettene. Bitene er de samme som <see cref="FilterMask"/> definerer, og
    /// ligger i bufferdataene — de endres aldri.
    /// </summary>
    public static readonly IReadOnlyList<FacetMaskOption> MaskFacets =
    [
        new("withImages", FilterMask.HasMediaFiles),
        new("sensitive", FilterMask.Sensitive),
        new("changedByAnnotation", FilterMask.ChangedByAnnotation),
        new("uncertainIdentification", FilterMask.UncertainIdentification),
        new("introduced", FilterMask.Introduced),
    ];

    private static FacetDimension Facet(string key, byte dimensionId)
    {
        var dimension = AreaCountCacheDimensions.All.SingleOrDefault(d => d.Id == dimensionId)
            ?? throw new InvalidOperationException(
                $"Fasetten «{key}» peker på dimensjon {dimensionId}, som ikke finnes i " +
                "AreaCountCacheDimensions. Registeret er omnummerert uten at fasettene " +
                "fulgte med.");

        // Fasetten grupperes på én kolonne og summeres over bøttene sine. Det holder bare
        // for enkeltverdi-dimensjoner: en observasjon med flere verdier ville blitt talt
        // én gang per verdi, og et intervall har ingen liste å vise.
        if (dimension.Kind != CacheDimensionKind.SingleValue)
            throw new InvalidOperationException(
                $"Fasetten «{key}» peker på dimensjonen «{dimension.Name}», som er " +
                $"{dimension.Kind}. Bare {nameof(CacheDimensionKind.SingleValue)} kan være fasett.");

        return new FacetDimension(key, dimension);
    }

    /// <summary>Fasettdimensjonen med denne nøkkelen, eller null.</summary>
    public static FacetDimension? FindDimension(string key) =>
        All.SingleOrDefault(f => f.Key == key);

    /// <summary>Maskefasetten med denne nøkkelen, eller null.</summary>
    public static FacetMaskOption? FindMaskFacet(string key) =>
        MaskFacets.SingleOrDefault(f => f.Key == key);

    /// <summary>
    /// Formen på oppslaget for én dimensjonsfasett: grupper på fasettens bøtte, begrenset
    /// av de andre aktive dimensjonene.
    /// </summary>
    public static FacetLookup ResolveDimensionFacet(FacetDimension facet, CacheDimensionContext context)
    {
        var mask = FilterMask.Select(context.Filter);

        // Maksimalt én annen dimensjon: fasetten bruker selv opp den ene siden av paret,
        // så to andre ville krevd et tredje bøttenivå.
        if (!TryCollectOthers(context, facet.DimensionId, maxOthers: 1, out var others))
            return FacetLookup.NotCovered;

        if (others.Count == 0)
        {
            return new FacetLookup
            {
                Level = FacetLookupLevel.Level1,
                GroupByDimensionId = facet.DimensionId,
                Mask = mask,
            };
        }

        var other = others[0];
        return new FacetLookup
        {
            Level = FacetLookupLevel.Level2,
            GroupByDimensionId = facet.DimensionId,
            Constraints = others,
            Mask = mask,
            PairId = AreaCountCacheDimensions.GetPairId(facet.DimensionId, other.Dimension.Id),
            GroupByIsBucketA = facet.DimensionId < other.Dimension.Id,
        };
    }

    /// <summary>
    /// Formen på oppslaget for ett bool-alternativ: én sum, begrenset av de aktive
    /// dimensjonene, med biten satt til alternativets verdi.
    ///
    /// Ingen dimensjon brukes opp av fasetten selv, så her er to aktive dimensjoner
    /// fortsatt dekket.
    /// </summary>
    public static FacetLookup ResolveMaskFacet(FacetMaskOption facet, bool value, CacheDimensionContext context)
    {
        var mask = FilterMask.WithBit(FilterMask.Select(context.Filter), facet.Bit, value);

        if (!TryCollectOthers(context, excludeDimensionId: null, maxOthers: 2, out var others))
            return FacetLookup.NotCovered;

        var level = others.Count switch
        {
            0 => FacetLookupLevel.Level0,
            1 => FacetLookupLevel.Level1,
            _ => FacetLookupLevel.Level2,
        };

        return new FacetLookup
        {
            Level = level,
            Constraints = others,
            Mask = mask,
            PairId = others.Count == 2
                ? AreaCountCacheDimensions.GetPairId(others[0].Dimension.Id, others[1].Dimension.Id)
                : null,
        };
    }

    /// <summary>
    /// Leser de aktive dimensjonene ut av filteret, sortert på id, og sier fra hvis
    /// oppslaget ikke kan besvares fra bufferen i det hele tatt.
    /// </summary>
    private static bool TryCollectOthers(
        CacheDimensionContext context,
        byte? excludeDimensionId,
        int maxOthers,
        out List<FacetConstraint> others)
    {
        others = [];

        // Katalognummer er ikke en dimensjon og kan ikke bli det: ObservationIds er en
        // vilkårlig liste løst opp av typeahead-endepunktet, uten et endelig vokabular å
        // bøtte på. Samme forbehold som i AreaCountCacheService.
        if (context.Filter.ObservationIds?.Length > 0)
            return false;

        // Matrikkelenheter er lokasjoner fra Kartverket, ikke bøtter i bufferen.
        if (context.Filter.CadastralUnitIds?.Length > 0)
            return false;

        foreach (var dimension in AreaCountCacheDimensions.All)
        {
            if (dimension.Id == excludeDimensionId)
                continue;

            var selection = dimension.Select(context);

            // Dimensjonen er i bruk, men kan ikke besvares herfra — f.eks. et takson under
            // ordensnivå. Hele oppslaget faller gjennom; å svare på resten av filteret og
            // ignorere denne ville gitt for høye tall.
            if (!selection.IsCacheable)
                return false;

            if (!selection.IsActive)
                continue;

            if (others.Count == maxOthers)
                return false;

            others.Add(new FacetConstraint(dimension, selection.Buckets!));
        }

        // AreaCountCacheDimensions.All er deklarert i id-rekkefølge, så lista er allerede
        // sortert. Sorteringen står likevel her fordi BucketA/BucketB-tilordningen hviler
        // på den, og en omstokking i registeret ellers ville flyttet tallene uten at noe
        // feilet.
        others.Sort((a, b) => a.Dimension.Id.CompareTo(b.Dimension.Id));
        return true;
    }

    /// <summary>
    /// Radformen i fasett-tabellene, uavhengig av dimensjonsregisteret. Se
    /// <c>AreaCountCacheDimensions.RowLayoutVersion</c> for hvorfor den er med.
    /// </summary>
    private const uint RowLayoutVersion = 1;

    /// <summary>
    /// Beskriver formen på fasett-bufferen. Oppslaget nekter å bruke en buffer med annen
    /// versjon enn koden som leser den.
    ///
    /// Bygger på <see cref="AreaCountCacheDimensions.SchemaVersion"/>, fordi bøttene og
    /// pargruppe-id-ene er de samme — endres de, må fasett-bufferen bygges om selv om
    /// ingenting i dette registeret er rørt. I tillegg hashes fasettsettet: legges en
    /// fasett til, finnes det ingen rader for den i en buffer bygget før endringen, og
    /// oppslaget ville lest «ingen rader» som null treff i hele gruppen.
    ///
    /// Endres denne, må @SchemaVersion i BuildFacetCountCache.sql oppdateres og bufferen
    /// bygges på nytt.
    /// </summary>
    public static int SchemaVersion { get; } = ComputeSchemaVersion();

    private static int ComputeSchemaVersion()
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            var hash = offsetBasis;

            hash = (hash ^ RowLayoutVersion) * prime;
            hash = (hash ^ (uint)AreaCountCacheDimensions.SchemaVersion) * prime;

            foreach (var facet in All.OrderBy(f => f.DimensionId))
            {
                hash = (hash ^ facet.DimensionId) * prime;
                foreach (var ch in facet.Key)
                    hash = (hash ^ ch) * prime;
            }

            foreach (var facet in MaskFacets.OrderBy(f => f.Bit))
            {
                hash = (hash ^ facet.Bit) * prime;
                foreach (var ch in facet.Key)
                    hash = (hash ^ ch) * prime;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
