using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Core.Application.Services;

/// <summary>Hvordan en dimensjons bøtter velges ut fra filteret.</summary>
public enum CacheDimensionKind
{
    /// <summary>Én kolonne med én verdi per observasjon. Summering over valgte verdier er eksakt.</summary>
    SingleValue,

    /// <summary>Ordnet verdi som filtreres med fra/til i stedet for medlemskap.</summary>
    Range,

    /// <summary>
    /// En observasjon kan ha flere verdier. Bøtta er hele mengden observasjonen tilhører,
    /// og filterverdiene må slås opp i AreaCountCacheBucketMember først.
    /// </summary>
    MultiValue,
}

/// <summary>Bøttene et filter peker ut innenfor én dimensjon.</summary>
public abstract record BucketSelection;

/// <summary>Et eksplisitt sett. For MultiValue er verdiene medlems-id-er, ikke bøtte-id-er.</summary>
public sealed record BucketSet(IReadOnlyList<int> Values) : BucketSelection;

/// <summary>
/// Et intervall over bøtte-id-en. <paramref name="Months"/> brukes kun av periode, der
/// bøtta er år * 100 + måned og månedsfilteret blir et restledd på modulo 100.
/// </summary>
public sealed record BucketRange(int? From, int? To, IReadOnlyList<int>? Months) : BucketSelection;

/// <summary>Resultatet av å lese én dimensjon ut av et filter.</summary>
public readonly record struct DimensionSelection(bool IsActive, bool IsCacheable, BucketSelection? Buckets)
{
    /// <summary>Dimensjonen er ikke i bruk i dette filteret.</summary>
    public static DimensionSelection Inactive => new(false, true, null);

    /// <summary>Dimensjonen er i bruk, men kan ikke besvares fra bufferen. Hele oppslaget faller gjennom.</summary>
    public static DimensionSelection NotCacheable => new(true, false, null);

    public static DimensionSelection Of(BucketSelection buckets) => new(true, true, buckets);
}

/// <summary>Det et utvalg trenger for å leses ut av et filter.</summary>
public sealed record CacheDimensionContext(
    LocationSearchFilterDto Filter,
    ITaxonHierarchyService TaxonHierarchy,
    IAreaHierarchyService AreaHierarchy);

/// <summary>
/// Én dimensjon i områdebufferen.
/// </summary>
/// <param name="Id">Stabil id. Endres aldri — den ligger i bufferdataene.</param>
/// <param name="Name">Navn for logging og feilsøking.</param>
/// <param name="Kind">Styrer hvordan <paramref name="Select"/>-resultatet tolkes.</param>
/// <param name="SqlExpression">
/// Uttrykket byggejobben grupperer på. Står her og ikke i jobben slik at dimensjonen
/// beskrives ett sted; en dimensjon lagt til i lista er dermed automatisk med i bygget.
/// </param>
/// <param name="Select">Leser dimensjonen ut av filteret.</param>
public sealed record CacheDimension(
    byte Id,
    string Name,
    CacheDimensionKind Kind,
    string SqlExpression,
    Func<CacheDimensionContext, DimensionSelection> Select);

/// <summary>
/// Dimensjonene områdebufferen dekker, og pargruppe-id-ene for toernivået.
///
/// GEOGRAFI ER ÉN DIMENSJON
/// Kommune, fylke, Svalbard, verneområde og havområde er ikke fem dimensjoner, men én.
/// <c>ComputeFilteredAreaCounts</c> ORer dem sammen i én semi-join, så et utvalg over
/// flere av dem er en union — og en union lar seg ikke sette sammen av separate
/// dimensjoner uten et korreksjonsledd som ville krevd et tredje bøttenivå.
///
/// Målt alternativ: splittet i kommune/fylke og vern/hav/Svalbard ble toernivået
/// 4 316 084 rader mot 3 630 515 for én samlet dimensjon — altså større, ikke mindre,
/// fordi splittingen dobler antall par mot alle de andre dimensjonene.
///
/// Geografi har ingen NULL-bøtte: målt 0 indeksrader hører til observasjoner uten
/// område. Hver observasjon ligger i minst ett.
///
/// Katalognummer er heller ikke med: <c>ObservationIds</c> er en vilkårlig liste løst opp av
/// typeahead-endepunktet, uten noe endelig vokabular å bøtte på. Den trenger det heller
/// ikke — id-ene er allerede slått opp, så filteret er et seek på klyngeindeksen.
///
/// TAKSON
/// Kun ordensnivå. Alle taksontilfeller over ett sekund er Class eller høyere og
/// konverteres allerede til ordener av <c>ApplyTaxonFilterToEntityIndex</c>. Familie
/// (757 ms), slekt (742 ms) og art (725 ms) ligger i den raske enden og ville kostet
/// 865 000 ekstra bufferrader for ingen gevinst.
/// </summary>
public static class AreaCountCacheDimensions
{
    /// <summary>
    /// Bøtta for rader der kildekolonnen er NULL. Velges aldri av et filter — et filter
    /// peker alltid ut konkrete verdier eller et intervall — men byggejobben trenger et
    /// sted å legge radene, og intervalloppslag må kunne utelukke dem eksplisitt.
    /// </summary>
    public const int NullBucket = int.MinValue;

    /// <summary>Rangnivået takson-dimensjonen er bygget på (orden).</summary>
    public const int TaxonRankOrder = 11;

    public static readonly IReadOnlyList<CacheDimension> All =
    [
        new(1, "Bilder", CacheDimensionKind.SingleValue, "HasMediaFiles",
            c => c.Filter.WithImages is { } withImages
                ? DimensionSelection.Of(new BucketSet([withImages ? 1 : 0]))
                : DimensionSelection.Inactive),

        new(2, "Regstatus", CacheDimensionKind.SingleValue, "RegistrationStatusId",
            c => c.Filter.RegistrationStatusId is { } status
                ? DimensionSelection.Of(new BucketSet([status]))
                : DimensionSelection.Inactive),

        // Atferd er tinyint på indekstabellen. Verdiene filtreres til tinyint-området
        // før de brukes, akkurat som i ApplyCommonFilters: C# caster unchecked, så
        // (byte)257 ville blitt 1. Er alle utenfor området blir bøttesettet tomt, og
        // oppslaget returnerer ingen treff — aldri ufiltrert.
        new(3, "Atferd", CacheDimensionKind.SingleValue, "BehaviorId",
            c => c.Filter.BehaviorIds?.Length > 0
                ? DimensionSelection.Of(new BucketSet(
                    c.Filter.BehaviorIds.Where(id => id is >= byte.MinValue and <= byte.MaxValue).Distinct().ToArray()))
                : DimensionSelection.Inactive),

        new(4, "Funntype", CacheDimensionKind.SingleValue, "BasisOfRecordId",
            c => Set(c.Filter.BasisOfRecordIds)),

        new(5, "Kategori", CacheDimensionKind.SingleValue, "CategoryId",
            c => Set(c.Filter.CategoryIds)),

        new(6, "Institusjon", CacheDimensionKind.SingleValue, "InstitutionOrgId",
            c => Set(c.Filter.OrganizationIds)),

        new(7, "Taksongruppe", CacheDimensionKind.SingleValue, "TaxonGroupId",
            c => Set(c.Filter.TaxonGroupIds)),

        new(8, "Takson", CacheDimensionKind.SingleValue, "OrderTaxonId", SelectTaxonOrders),

        new(9, "Datasett", CacheDimensionKind.SingleValue, "DatasetOrgId",
            c => c.Filter.DatasetOrgId is { } datasetOrgId
                ? DimensionSelection.Of(new BucketSet([datasetOrgId]))
                : DimensionSelection.Inactive),

        // Bøtta er år * 100 + måned. Årsintervallet blir et rekkeviddeoppslag på
        // klyngeindeksen, månedsutvalget et restledd på modulo 100.
        new(10, "Periode", CacheDimensionKind.Range,
            "YEAR(DateTimeCollected) * 100 + MONTH(DateTimeCollected)", SelectPeriod),

        new(11, "Koordpresisjon", CacheDimensionKind.Range, "CoordinatePrecisionInMeters",
            c => c.Filter.CoordinatePrecision is { } precision && (precision.From.HasValue || precision.To.HasValue)
                ? DimensionSelection.Of(new BucketRange(precision.From, precision.To, null))
                : DimensionSelection.Inactive),

        new(12, "Geografi", CacheDimensionKind.MultiValue, "EntityTypeId IN (1,2,3,4,6)", SelectGeography),

        new(13, "Prosjekt", CacheDimensionKind.MultiValue, "ObservationProject.ProjectOrgId",
            c => c.Filter.ProjectOrgId is { } projectOrgId
                ? DimensionSelection.Of(new BucketSet([projectOrgId]))
                : DimensionSelection.Inactive),
    ];

    /// <summary>
    /// Områdetypene geografi-dimensjonen dekker, slik semi-joinen i
    /// ComputeFilteredAreaCounts gjør det.
    /// </summary>
    public const int AreaTypeMunicipality = 1;
    public const int AreaTypeCounty = 2;
    public const int AreaTypeRestricted = 3;
    public const int AreaTypeOcean = 4;
    public const int AreaTypeSvalbard = 6;

    /// <summary>
    /// Medlems-id-ene i geografi-dimensjonen må skille på områdetype: kommune 4206 og
    /// verneområde 4206 er ulike områder. De pakkes derfor som type * denne faktoren
    /// pluss id. Største observerte id er 5636 (kommune), så 1 000 000 gir rikelig rom
    /// og holder tallet lesbart i feilsøking: 1004206 er «kommune 4206».
    /// </summary>
    public const int AreaTypeFactor = 1_000_000;

    public static int PackAreaMember(int areaTypeId, int entityId) => areaTypeId * AreaTypeFactor + entityId;

    /// <summary>
    /// Leser hele det geografiske utvalget ut som medlems-id-er.
    ///
    /// HVORFOR ÉN DIMENSJON
    /// Semi-joinen ORer kommune, fylke, Svalbard, verneområde og havområde sammen i ett
    /// uttrykk. Et utvalg som spenner over flere av dem er altså én union, ikke flere
    /// uavhengige filtre — og en union lar seg ikke sette sammen av separate dimensjoner
    /// uten et korreksjonsledd som ville krevd et tredje bøttenivå.
    ///
    /// Som én dimensjon forsvinner problemet: observasjonen ligger i nøyaktig én bøtte,
    /// bøtta inneholder alle områdene den tilhører, og «kommune X eller havområde Y» blir
    /// summen av bøttene hvis mengde skjærer utvalget. Hver observasjon telles én gang.
    ///
    /// Det er også det som gjør overlappene ufarlige: kommune og fylke overlapper 100 %,
    /// havområde og Svalbard 100 %, og kommune og havområde med 395 observasjoner. Ingen
    /// av dem krever at koden kjenner hierarkiet.
    ///
    /// FYLKE TREFFER OGSÅ SVALBARD
    /// Semi-joinen matcher <c>EntityTypeId = 6</c> mot <c>countyIds</c>, ikke mot en egen
    /// liste. Et fylkevalg kan derfor treffe Svalbard-områder, og fylke-Fid-ene slås opp
    /// mot begge typene. Uten det ville bufferen svart noe annet enn tellingen.
    /// </summary>
    private static DimensionSelection SelectGeography(CacheDimensionContext c)
    {
        var filter = c.Filter;
        var members = new List<int>();

        foreach (var entityId in c.AreaHierarchy.FidsToEntityIds(filter.MunicipalityIds))
            members.Add(PackAreaMember(AreaTypeMunicipality, entityId));

        foreach (var entityId in c.AreaHierarchy.FidsToEntityIds(filter.CountyIds))
        {
            members.Add(PackAreaMember(AreaTypeCounty, entityId));
            members.Add(PackAreaMember(AreaTypeSvalbard, entityId));
        }

        foreach (var entityId in c.AreaHierarchy.RestrictedAreaFidsToEntityIds(filter.RestrictedAreaIds))
            members.Add(PackAreaMember(AreaTypeRestricted, entityId));

        foreach (var entityId in c.AreaHierarchy.FidsToEntityIds(filter.OceanAreaIds))
            members.Add(PackAreaMember(AreaTypeOcean, entityId));

        return members.Count == 0
            ? DimensionSelection.Inactive
            : DimensionSelection.Of(new BucketSet(members.Distinct().ToArray()));
    }

    private static DimensionSelection Set(int[]? values) =>
        values?.Length > 0
            ? DimensionSelection.Of(new BucketSet(values.Distinct().ToArray()))
            : DimensionSelection.Inactive;

    /// <summary>
    /// Løser taksonutvalget til ordens-id-er, eller gir opp.
    ///
    /// Bufferen har ett tall per (orden, område). Et takson som ikke kan uttrykkes som
    /// en mengde ordener — art, slekt, familie, eller et mellomnivå som må gå via
    /// ObservationTaxonHierarchy — kan ikke besvares herfra, og hele oppslaget faller
    /// gjennom til spørringen.
    ///
    /// Utvalget er normalisert av TaxonFilterNormalization før dette, så et takson og
    /// dets egen etterkommer opptrer ikke sammen. Uten den normaliseringen ville to
    /// ordener under samme klasse kunne telt de samme radene to ganger.
    /// </summary>
    private static DimensionSelection SelectTaxonOrders(CacheDimensionContext c)
    {
        var taxonIds = c.TaxonHierarchy.RemoveRedundantDescendants(c.Filter.TaxonIds);
        if (taxonIds is not { Length: > 0 })
            return DimensionSelection.Inactive;

        var orderIds = new HashSet<int>();
        foreach (var taxonId in taxonIds)
        {
            var rankId = c.TaxonHierarchy.GetTaxonRankId(taxonId);
            if (rankId is null)
                return DimensionSelection.NotCacheable;

            if (rankId == TaxonRankOrder)
            {
                orderIds.Add(taxonId);
                continue;
            }

            // Under ordensnivå: art, slekt, familie og alle mellomnivåer. De finnes ikke
            // som bøtter, og en orden ville vært for vid.
            if (rankId > TaxonRankOrder)
                return DimensionSelection.NotCacheable;

            var descendants = c.TaxonHierarchy.GetDescendantIdsAtRank(taxonId, TaxonRankOrder);
            if (descendants.Count == 0)
                return DimensionSelection.NotCacheable;

            orderIds.UnionWith(descendants);
        }

        return DimensionSelection.Of(new BucketSet(orderIds.ToArray()));
    }

    /// <summary>
    /// Oversetter årsintervallet til bøtteintervallet. Åpne ender beholdes åpne, slik at
    /// bare NULL-bøtta utelukkes — samme som spørringen, der en NULL-dato aldri matcher
    /// en datosammenligning.
    /// </summary>
    private static DimensionSelection SelectPeriod(CacheDimensionContext c)
    {
        var period = c.Filter.Period;
        if (period is null || (period.From is null && period.To is null && !(period.Months?.Any() == true)))
            return DimensionSelection.Inactive;

        return DimensionSelection.Of(new BucketRange(
            period.From is { } fromYear ? fromYear * 100 + 1 : null,
            period.To is { } toYear ? toYear * 100 + 12 : null,
            period.Months?.Length > 0 ? period.Months.Distinct().ToArray() : null));
    }

    /// <summary>
    /// Pargruppe-id for toernivået. Nøkkelen er alltid (lavest id, høyest id), slik at
    /// rekkefølgen filtrene står i ikke påvirker oppslaget.
    /// </summary>
    private static readonly Dictionary<(byte A, byte B), byte> PairIds = BuildPairIds();

    private static Dictionary<(byte, byte), byte> BuildPairIds()
    {
        var ids = All.Select(d => d.Id).OrderBy(id => id).ToArray();
        var pairs = new Dictionary<(byte, byte), byte>();
        byte next = 1;

        for (var i = 0; i < ids.Length; i++)
            for (var j = i + 1; j < ids.Length; j++)
                pairs[(ids[i], ids[j])] = next++;

        return pairs;
    }

    /// <summary>Antall par bufferen dekker. 13 dimensjoner gir 78.</summary>
    public static int PairCount => PairIds.Count;

    /// <summary>
    /// Pargruppe-id for to dimensjoner, uavhengig av rekkefølge. Kalleren må sortere
    /// bøttene tilsvarende: BucketA hører til dimensjonen med lavest id.
    /// </summary>
    public static byte GetPairId(byte dimensionA, byte dimensionB) =>
        PairIds[dimensionA < dimensionB ? (dimensionA, dimensionB) : (dimensionB, dimensionA)];

    /// <summary>
    /// Beskriver formen på registeret. Legges en dimensjon til, forskyves pargruppe-id-ene,
    /// og en buffer bygget før endringen ville svart på feil par. Oppslaget nekter å bruke
    /// en buffer med annen versjon enn koden som leser den.
    ///
    /// Utregnet med FNV-1a, ikke string.GetHashCode: den siste er randomisert per prosess
    /// og ville gitt ny versjon ved hver omstart.
    /// </summary>
    public static int SchemaVersion { get; } = ComputeSchemaVersion();

    private static int ComputeSchemaVersion()
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            var hash = offsetBasis;

            foreach (var dimension in All.OrderBy(d => d.Id))
            {
                hash = (hash ^ dimension.Id) * prime;
                hash = (hash ^ (uint)dimension.Kind) * prime;
                foreach (var ch in dimension.Name)
                    hash = (hash ^ ch) * prime;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
