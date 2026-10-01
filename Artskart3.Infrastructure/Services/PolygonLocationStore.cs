using System.Diagnostics;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// KOLONNER, IKKE OBJEKTER
/// Alt ligger i parallelle arrayer indeksert på en tett observasjonsindeks.
/// 3,9 millioner objekter med ett felt hver ville kostet mer i header enn i
/// data, og skanningen ville blitt pekerjaging i stedet for sekvensiell lesing.
///
/// OBSERVASJONENE ER SORTERT PÅ LOKASJON
/// <see cref="_obsStart"/> gir start og slutt for hver lokasjons observasjoner,
/// slik at et kartutsnitt kan hoppe over hele lokasjoner uten å røre
/// observasjonene deres. For Oslo-utsnittet er 10 903 av 73 908 lokasjoner
/// aktuelle, så 85 % av observasjonene forbigås uten en eneste sammenligning.
/// Uten den grupperingen måtte hver av de 3,9 millionene i det minste vært lest.
///
/// WKT LIGGER SOM string, IKKE UTF-8
/// 157 MB mot 78 MB. Det dobbelte er betalt med vilje: DTO-en trenger en string,
/// så en byte-representasjon måtte dekodet og allokert per treff — nøyaktig den
/// kostnaden dette skal fjerne. Slik deles samme instans ut, og spørringen
/// kopierer bare en peker.
/// </remarks>
public sealed class PolygonLocationStore : IPolygonLocationStore
{
    private const int Ingen = int.MinValue;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly IAreaHierarchyService _areaHierarchy;
    private readonly ILogger<PolygonLocationStore> _logger;

    private volatile bool _ready;
    private string _beskrivelse = "ikke bygget";

    // --- per lokasjon, tett indeks 0..n-1 -----------------------------------
    private int[] _locId = [];
    private int[] _east = [];
    private int[] _north = [];
    private string?[] _locality = [];
    private string[] _wkt = [];
    private double[] _bbMinX = [], _bbMinY = [], _bbMaxX = [], _bbMaxY = [];
    private bool[] _erRutenett = [];
    private int[] _obsStart = [];

    // --- per observasjon, sortert på lokasjon --------------------------------
    private int[] _obsId = [];
    private byte[] _regStatus = [];
    private byte[] _behavior = [];          // 0 = ingen
    private bool[] _harBilder = [];
    private int[] _funntype = [];
    private int[] _kategori = [];           // Ingen = null
    private int[] _institusjon = [];        // Ingen = null
    private int[] _taksongruppe = [];
    private int[] _datasett = [];           // Ingen = null
    private int[] _koordpresisjon = [];     // Ingen = null
    private long[] _samletTicks = [];       // long.MinValue = null
    private byte[] _maaned = [];            // 0 = null
    private int[] _taksonIdx = [];          // Ingen = ukjent takson
    private int[] _geoBucketIdx = [];       // Ingen = ingen geografi
    private int[] _projBucketIdx = [];      // Ingen = ingen prosjekt

    // --- oppslag ------------------------------------------------------------
    private Dictionary<int, int[]> _taksaUnder = new();   // forfader -> tette taksonindekser
    private int _taksonAntall;
    private Dictionary<int, int[]> _geoBucketsForMedlem = new();
    private Dictionary<int, int[]> _projBucketsForMedlem = new();
    private int _geoBucketAntall, _projBucketAntall;

    public PolygonLocationStore(
        IServiceScopeFactory scopeFactory,
        ITaxonHierarchyService taxonHierarchy,
        IAreaHierarchyService areaHierarchy,
        ILogger<PolygonLocationStore> logger)
    {
        _scopeFactory = scopeFactory;
        _taxonHierarchy = taxonHierarchy;
        _areaHierarchy = areaHierarchy;
        _logger = logger;
    }

    /// <summary>
    /// Nødbryter. Settes miljøvariabelen til 1, svarer lageret aldri, og alle
    /// polygonkall går mot databasen. Finnes både for å kunne slå av i drift
    /// uten deploy, og for å kunne kjøre de to stiene mot hverandre og
    /// sammenligne svarene — som er den eneste måten å bevise at de er like.
    /// </summary>
    public const string AvBryterVariabel = "ARTSKART_DISABLE_POLYGON_MEMORY";

    private static readonly bool AvSlaatt =
        Environment.GetEnvironmentVariable(AvBryterVariabel) == "1";

    public bool IsReady => _ready && !AvSlaatt;

    public string Describe() => _beskrivelse;

    // =======================================================================
    // Spørring
    // =======================================================================

    /// <inheritdoc />
    public IReadOnlyList<LocationPolygonDto>? TryQuery(LocationSearchFilterDto filter, int maxResults)
    {
        if (!IsReady) return null;

        var p = BuildPredicate(filter);

        // Et filter som ikke kan treffe noe. Tomt, aldri ufiltrert.
        if (p.AlltidTomt) return [];

        var envelope = filter.Envelope;
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        var harUtsnitt = envelope is not null;
        if (harUtsnitt)
        {
            // Samme avkorting som LocationsForFilter.
            minX = (int)envelope!.MinX; maxX = (int)envelope.MaxX;
            minY = (int)envelope.MinY; maxY = (int)envelope.MaxY;
        }

        var antallLok = _locId.Length;
        var treff = new List<(int Count, int LocationId, int Index)>(1024);

        for (var loc = 0; loc < antallLok; loc++)
        {
            if (harUtsnitt)
            {
                var e = _east[loc];
                if (e < minX || e > maxX) continue;
                var n = _north[loc];
                if (n < minY || n > maxY) continue;
            }

            var slutt = _obsStart[loc + 1];
            var antall = 0;
            for (var i = _obsStart[loc]; i < slutt; i++)
                if (Matcher(i, in p)) antall++;

            if (antall > 0) treff.Add((antall, _locId[loc], loc));
        }

        // Samme rekkefølge som databasestien: antall synkende, deretter
        // LocationId stigende. Tiebreakeren er ikke pynt — uten den ville de to
        // stiene valgt ulike lokasjoner ved like tall.
        treff.Sort(static (a, b) =>
        {
            var c = b.Count.CompareTo(a.Count);
            return c != 0 ? c : a.LocationId.CompareTo(b.LocationId);
        });

        var tak = Math.Min(maxResults, treff.Count);
        var resultat = new List<LocationPolygonDto>(tak);

        // Rektangel- og boksfilteret kjører ETTER TOP, som i databasestien.
        // Det er derfor endepunktet ber om 5 000 og leverer 4 452. Flyttes
        // filtreringen før kuttet, endres hvilke polygoner som returneres.
        for (var k = 0; k < tak; k++)
        {
            var (antall, locId, loc) = treff[k];

            if (_erRutenett[loc]) continue;

            if (harUtsnitt &&
                (_bbMaxX[loc] < envelope!.MinX || _bbMinX[loc] > envelope.MaxX ||
                 _bbMaxY[loc] < envelope.MinY || _bbMinY[loc] > envelope.MaxY))
                continue;

            resultat.Add(new LocationPolygonDto
            {
                LocationId = locId,
                Locality = _locality[loc],
                WktPolygon = _wkt[loc],
                ObservationCount = antall,
            });
        }

        // REKKEFØLGEN MATCHER DATABASESTIEN, SOM ER TILFELDIG
        // Der hentes geometrien med «WHERE Id IN (...)» etter at TOP-en er
        // tatt, og SQL Server leverer den i klyngeindeksrekkefølge — altså
        // sortert på LocationId, ikke på antall. Det er ingen kontrakt, bare
        // en bieffekt, men å bevare den gjør dette til en ren ytelsesendring:
        // sammenligningen av de to stiene blir eksakt, rad for rad, og ingen
        // frontend kan overraskes av at rekkefølgen plutselig ble en annen.
        //
        // Utvalget er allerede avgjort over; dette sorterer bare det som skal
        // returneres.
        resultat.Sort(static (a, b) => a.LocationId.CompareTo(b.LocationId));

        return resultat;
    }

    /// <summary>
    /// Predikatet i en form skanningen kan lese uten allokering eller
    /// virtuelle kall. Bool-flagg framfor nullable, og tette bool-arrayer
    /// framfor mengder, fordi dette leses opptil 3,9 millioner ganger.
    /// </summary>
    private readonly struct Predikat
    {
        public bool AlltidTomt { get; init; }

        public HashSet<int>? TaksongruppeSet { get; init; }
        public HashSet<int>? Kategori { get; init; }
        public HashSet<int>? Funntype { get; init; }
        public HashSet<int>? Institusjon { get; init; }
        public HashSet<byte>? Atferd { get; init; }
        public HashSet<int>? ObservasjonsIder { get; init; }
        public HashSet<byte>? Maaneder { get; init; }

        public byte RegStatus { get; init; }         // 0 = ikke satt
        public int Datasett { get; init; }           // Ingen = ikke satt
        public bool HarBilderSatt { get; init; }
        public bool HarBilder { get; init; }

        public int KoordFra { get; init; }           // Ingen = ikke satt
        public int KoordTil { get; init; }
        public long TicksFra { get; init; }          // long.MinValue = ikke satt
        public long TicksTil { get; init; }

        public bool[]? Takson { get; init; }         // tett taksonindeks -> treffer
        public bool[]? GeoBucket { get; init; }      // tett bucketindeks -> treffer
        public bool[]? ProjBucket { get; init; }
    }

    private bool Matcher(int i, in Predikat p)
    {
        if (p.TaksongruppeSet is { } tg && !tg.Contains(_taksongruppe[i])) return false;

        if (p.Kategori is { } kat)
        {
            var v = _kategori[i];
            if (v == Ingen || !kat.Contains(v)) return false;
        }

        if (p.Funntype is { } ft && !ft.Contains(_funntype[i])) return false;

        if (p.Institusjon is { } inst)
        {
            var v = _institusjon[i];
            if (v == Ingen || !inst.Contains(v)) return false;
        }

        if (p.Atferd is { } atf)
        {
            var v = _behavior[i];
            if (v == 0 || !atf.Contains(v)) return false;
        }

        if (p.RegStatus != 0 && _regStatus[i] != p.RegStatus) return false;

        if (p.Datasett != Ingen && _datasett[i] != p.Datasett) return false;

        if (p.HarBilderSatt && _harBilder[i] != p.HarBilder) return false;

        if (p.KoordFra != Ingen)
        {
            var v = _koordpresisjon[i];
            if (v == Ingen || v < p.KoordFra) return false;
        }
        if (p.KoordTil != Ingen)
        {
            var v = _koordpresisjon[i];
            if (v == Ingen || v > p.KoordTil) return false;
        }

        if (p.TicksFra != long.MinValue)
        {
            var v = _samletTicks[i];
            if (v == long.MinValue || v < p.TicksFra) return false;
        }
        if (p.TicksTil != long.MinValue)
        {
            var v = _samletTicks[i];
            if (v == long.MinValue || v > p.TicksTil) return false;
        }

        if (p.Maaneder is { } mnd)
        {
            var v = _maaned[i];
            if (v == 0 || !mnd.Contains(v)) return false;
        }

        if (p.ObservasjonsIder is { } ids && !ids.Contains(_obsId[i])) return false;

        if (p.Takson is { } tax)
        {
            var t = _taksonIdx[i];
            if (t == Ingen || !tax[t]) return false;
        }

        if (p.GeoBucket is { } geo)
        {
            var b = _geoBucketIdx[i];
            if (b == Ingen || !geo[b]) return false;
        }

        if (p.ProjBucket is { } prj)
        {
            var b = _projBucketIdx[i];
            if (b == Ingen || !prj[b]) return false;
        }

        return true;
    }

    /// <summary>
    /// Oversetter filteret. Hver gren speiler ApplyCommonFilters ordrett —
    /// inkludert vaktene der, som at atferds-ID-er utenfor tinyint gir tomt og
    /// ikke ufiltrert.
    /// </summary>
    private Predikat BuildPredicate(LocationSearchFilterDto f)
    {
        var tomt = false;

        HashSet<byte>? atferd = null;
        if (f.BehaviorIds?.Length > 0)
        {
            atferd = f.BehaviorIds.Where(x => x is >= byte.MinValue and <= byte.MaxValue)
                                  .Select(x => (byte)x).ToHashSet();
            if (atferd.Count == 0) tomt = true;
        }

        HashSet<byte>? maaneder = null;
        if (f.Period?.Months?.Length > 0)
        {
            maaneder = f.Period.Months.Where(m => m is >= 1 and <= 12).Select(m => (byte)m).ToHashSet();
            if (maaneder.Count == 0) tomt = true;
        }

        // Takson: samme normalisering og samme hierarkioppslag som
        // ApplyCommonFilters, men mot forfedrene vi bygget ved oppstart.
        bool[]? takson = null;
        var taxonIds = _taxonHierarchy.RemoveRedundantDescendants(f.TaxonIds);
        if (taxonIds is { Length: > 0 })
        {
            takson = new bool[_taksonAntall];
            var noenTreff = false;
            foreach (var id in taxonIds)
            {
                if (!_taksaUnder.TryGetValue(id, out var under)) continue;
                foreach (var idx in under) takson[idx] = true;
                noenTreff = true;
            }
            if (!noenTreff) tomt = true;
        }

        // Geografi: ORes sammen som i semi-joinen, og fylkes-ID-er slås opp mot
        // BÅDE fylke og Svalbard — akkurat som ellers i løsningen.
        bool[]? geo = null;
        var harOmraade = f.MunicipalityIds?.Length > 0 || f.CountyIds?.Length > 0
                         || f.RestrictedAreaIds?.Length > 0 || f.OceanAreaIds?.Length > 0;
        if (harOmraade)
        {
            var medlemmer = new List<int>();
            foreach (var id in _areaHierarchy.FidsToEntityIds(f.MunicipalityIds))
                medlemmer.Add(AreaCountCacheDimensions.PackAreaMember(AreaCountCacheDimensions.AreaTypeMunicipality, id));
            foreach (var id in _areaHierarchy.FidsToEntityIds(f.CountyIds))
            {
                medlemmer.Add(AreaCountCacheDimensions.PackAreaMember(AreaCountCacheDimensions.AreaTypeCounty, id));
                medlemmer.Add(AreaCountCacheDimensions.PackAreaMember(AreaCountCacheDimensions.AreaTypeSvalbard, id));
            }
            foreach (var id in _areaHierarchy.RestrictedAreaFidsToEntityIds(f.RestrictedAreaIds))
                medlemmer.Add(AreaCountCacheDimensions.PackAreaMember(AreaCountCacheDimensions.AreaTypeRestricted, id));
            foreach (var id in _areaHierarchy.FidsToEntityIds(f.OceanAreaIds))
                medlemmer.Add(AreaCountCacheDimensions.PackAreaMember(AreaCountCacheDimensions.AreaTypeOcean, id));

            geo = new bool[_geoBucketAntall];
            var noen = false;
            foreach (var m in medlemmer.Distinct())
            {
                if (!_geoBucketsForMedlem.TryGetValue(m, out var buckets)) continue;
                foreach (var b in buckets) geo[b] = true;
                noen = true;
            }
            if (!noen) tomt = true;
        }

        bool[]? proj = null;
        if (f.ProjectOrgId is { } prosjektId)
        {
            proj = new bool[_projBucketAntall];
            if (_projBucketsForMedlem.TryGetValue(prosjektId, out var buckets))
                foreach (var b in buckets) proj[b] = true;
            else
                tomt = true;
        }

        return new Predikat
        {
            AlltidTomt = tomt,
            TaksongruppeSet = f.TaxonGroupIds?.Length > 0 ? f.TaxonGroupIds.ToHashSet() : null,
            Kategori = f.CategoryIds?.Length > 0 ? f.CategoryIds.ToHashSet() : null,
            Funntype = f.BasisOfRecordIds?.Length > 0 ? f.BasisOfRecordIds.ToHashSet() : null,
            Institusjon = f.OrganizationIds?.Length > 0 ? f.OrganizationIds.ToHashSet() : null,
            Atferd = atferd,
            ObservasjonsIder = f.ObservationIds?.Length > 0 ? f.ObservationIds.ToHashSet() : null,
            Maaneder = maaneder,
            RegStatus = f.RegistrationStatusId is { } rs ? (byte)rs : (byte)0,
            Datasett = f.DatasetOrgId ?? Ingen,
            HarBilderSatt = f.WithImages.HasValue,
            HarBilder = f.WithImages ?? false,
            KoordFra = f.CoordinatePrecision?.From ?? Ingen,
            KoordTil = f.CoordinatePrecision?.To ?? Ingen,
            TicksFra = f.Period?.From is { } fra ? new DateTime(fra, 1, 1).Ticks : long.MinValue,
            TicksTil = f.Period?.To is { } til ? new DateTime(til, 12, 31, 23, 59, 59).Ticks : long.MinValue,
            Takson = takson,
            GeoBucket = geo,
            ProjBucket = proj,
        };
    }

    // =======================================================================
    // Bygging
    // =======================================================================

    /// <inheritdoc />
    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        var klokke = Stopwatch.StartNew();
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        var lokIndeks = await LastLokasjonerAsync(context, cancellationToken);
        _logger.LogInformation("PolygonLocationStore: {Antall} lokasjoner etter {Sek}s",
            _locId.Length, klokke.Elapsed.TotalSeconds.ToString("F1"));

        var taksonIdx = await LastTaksonomiAsync(context, cancellationToken);
        _logger.LogInformation("PolygonLocationStore: {Takson} takson etter {Sek}s",
            _taksonAntall, klokke.Elapsed.TotalSeconds.ToString("F1"));

        await LastObservasjonerAsync(context, lokIndeks, taksonIdx, cancellationToken);
        _logger.LogInformation("PolygonLocationStore: {Geo} geobøtter, {Proj} prosjektbøtter",
            _geoBucketAntall, _projBucketAntall);

        _ready = true;
        _beskrivelse = $"{_locId.Length:N0} lokasjoner, {_obsId.Length:N0} observasjoner, " +
                       $"bygget på {klokke.Elapsed.TotalSeconds:F1}s";
        _logger.LogInformation("PolygonLocationStore klar: {Beskrivelse}", _beskrivelse);
    }

    /// <summary>
    /// Lokasjonene, med WKT, omsluttende boks og rutenettflagg regnet ut én
    /// gang. Det er her NTS-materialiseringen og AsText() betales — 130 ms per
    /// kall i databasestien, her én gang totalt.
    /// </summary>
    private async Task<Dictionary<int, int>> LastLokasjonerAsync(
        IArtsKartDbContext context, CancellationToken ct)
    {
        var typer = new[]
        {
            LocationGeometryType.Polygon, LocationGeometryType.MultiPolygon,
            LocationGeometryType.LineString, LocationGeometryType.MultiLineString,
        };

        var rader = await context.Set<Location>().AsNoTracking()
            .Where(l => typer.Contains(l.GeometryTypeId) && l.Geometry != null)
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.East, l.North, l.Locality, l.Geometry })
            .ToListAsync(ct);

        var n = rader.Count;
        _locId = new int[n]; _east = new int[n]; _north = new int[n];
        _locality = new string?[n]; _wkt = new string[n];
        _bbMinX = new double[n]; _bbMinY = new double[n];
        _bbMaxX = new double[n]; _bbMaxY = new double[n];
        _erRutenett = new bool[n];

        var indeks = new Dictionary<int, int>(n);
        for (var i = 0; i < n; i++)
        {
            var r = rader[i];
            _locId[i] = r.Id; _east[i] = r.East; _north[i] = r.North; _locality[i] = r.Locality;

            var wkt = r.Geometry!.AsText();
            _wkt[i] = wkt;
            _erRutenett[i] = LocationGeometryRules.IsRectangularPolygon(wkt);

            var env = r.Geometry.EnvelopeInternal;
            _bbMinX[i] = env.MinX; _bbMinY[i] = env.MinY;
            _bbMaxX[i] = env.MaxX; _bbMaxY[i] = env.MaxY;

            indeks[r.Id] = i;
        }

        return indeks;
    }

    /// <summary>
    /// Leser bøttemedlemskapet som forberedelsen nettopp bygget i tempdb.
    ///
    /// BØTTENE NUMMERERES HER, IKKE LÅNT FRA AreaCountCacheBucketMember
    /// Det var fristende å låne dem — områdebufferen har allerede nøyaktig
    /// denne strukturen — men det ville koblet minnelageret til at et annet
    /// byggeskript hadde kjørt. Var den tabellen tom, ville hvert geografi- og
    /// prosjektfilter stille returnert null polygoner i stedet for å feile.
    /// Bøttene er billige å regne ut, og selvstendighet er verdt mer enn
    /// gjenbruk her.
    ///
    /// Bøtta er HELE mengden områder en observasjon tilhører, ikke
    /// enkeltområdet. Summerer man per område i stedet, telles en observasjon i
    /// to verneområder to ganger når begge er valgt.
    /// </summary>
    private void LesBoetter(SqlDataReader leser, bool geografi)
    {
        var forMedlem = new Dictionary<int, List<int>>();
        var tett = new Dictionary<int, int>();

        while (leser.Read())
        {
            var bucketId = leser.GetInt32(0);
            var memberId = leser.GetInt32(1);

            if (!tett.TryGetValue(bucketId, out var idx))
                tett[bucketId] = idx = tett.Count;

            if (!forMedlem.TryGetValue(memberId, out var liste))
                forMedlem[memberId] = liste = [];
            liste.Add(idx);
        }

        var ferdig = forMedlem.ToDictionary(kv => kv.Key, kv => kv.Value.Distinct().ToArray());

        if (geografi) { _geoBucketsForMedlem = ferdig; _geoTett = tett; _geoBucketAntall = tett.Count; }
        else { _projBucketsForMedlem = ferdig; _projTett = tett; _projBucketAntall = tett.Count; }
    }

    private Dictionary<int, int> _geoTett = new();
    private Dictionary<int, int> _projTett = new();

    /// <summary>
    /// Bygger forfedrene per takson, og snur dem til «hvilke takson ligger
    /// under X». Uten den inverteringen måtte hver spørring gått gjennom alle
    /// takson for å finne treffene.
    ///
    /// Forfedrene leses fra ObservationTaxonHierarchy, som er nøyaktig det
    /// GetObservationIdsByTaxonHierarchy filtrerer mot. Hadde vi i stedet
    /// utledet dem fra takson-treet, kunne de to stiene divergert.
    /// </summary>
    private async Task<Dictionary<int, int>> LastTaksonomiAsync(IArtsKartDbContext context, CancellationToken ct)
    {
        const string sql = """
            SELECT DISTINCT o.TaxonId,
                h.KingdomTaxonId, h.SubkingdomTaxonId, h.PhylumTaxonId, h.SubphylumTaxonId,
                h.SuperclassTaxonId, h.ClassTaxonId, h.SubclassTaxonId, h.InfraclassTaxonId,
                h.CohortTaxonId, h.SuperorderTaxonId, h.OrderTaxonId, h.SuborderTaxonId,
                h.InfraorderTaxonId, h.SuperfamilyTaxonId, h.FamilyTaxonId, h.SubfamilyTaxonId,
                h.TribeTaxonId, h.SubtribeTaxonId, h.GenusTaxonId, h.SubgenusTaxonId,
                h.SectionTaxonId, h.SpeciesTaxonId, h.SubspeciesTaxonId, h.VarietyTaxonId,
                h.FormTaxonId, h.NotSetTaxonId
            FROM dbo.Observation o
            JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = o.Id
            JOIN dbo.Location l ON l.Id = o.LocationId
            WHERE l.GeometryTypeId IN (2,3,4,5)
            """;

        var tett = new Dictionary<int, int>();
        var forfedre = new Dictionary<int, HashSet<int>>();

        await using (var conn = NyTilkobling(context))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 0;

            await using var leser = await cmd.ExecuteReaderAsync(ct);
            while (await leser.ReadAsync(ct))
            {
                var taxonId = leser.GetInt32(0);
                if (!tett.ContainsKey(taxonId)) tett[taxonId] = tett.Count;

                if (!forfedre.TryGetValue(taxonId, out var sett))
                    forfedre[taxonId] = sett = [];

                for (var c = 1; c <= 26; c++)
                    if (!leser.IsDBNull(c)) sett.Add(leser.GetInt32(c));
            }
        }

        _taksonAntall = tett.Count;

        var under = new Dictionary<int, List<int>>();
        foreach (var (taxonId, sett) in forfedre)
        {
            var idx = tett[taxonId];
            foreach (var forfar in sett)
            {
                if (!under.TryGetValue(forfar, out var liste))
                    under[forfar] = liste = [];
                liste.Add(idx);
            }
        }

        _taksaUnder = under.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        return tett;
    }

    private async Task LastObservasjonerAsync(
        IArtsKartDbContext context, Dictionary<int, int> lokIndeks,
        Dictionary<int, int> taksonIdx, CancellationToken ct)
    {
        // Sortert på LocationId slik at CSR-oppsettet faller ut av lesingen.
        const string sql = """
            SELECT o.Id, o.LocationId, o.TaxonId, o.TaxonGroupId, o.CategoryId,
                   o.BasisOfRecordId, o.InstitutionOrgId, o.DatasetOrgId,
                   o.RegistrationStatusId, o.BehaviorId, o.HasMediaFiles,
                   o.CoordinatePrecisionInMeters, o.DateTimeCollected, o.MonthCollected,
                   g.BucketId AS GeoBucket, p.BucketId AS ProjBucket
            FROM dbo.Observation o
            JOIN dbo.Location l ON l.Id = o.LocationId
            LEFT JOIN #geo g ON g.ObservationId = o.Id
            LEFT JOIN #proj p ON p.ObservationId = o.Id
            WHERE l.GeometryTypeId IN (2,3,4,5)
            ORDER BY o.LocationId
            """;

        // Bøttetilordningen bygges i tempdb først; å gjøre den i spørringen
        // over ville kjørt STRING_AGG over hele indekstabellen per rad.
        const string forberedelse = """
            SET QUOTED_IDENTIFIER ON;
            CREATE TABLE #sig (ObservationId INT PRIMARY KEY, Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT);
            INSERT #sig
            SELECT i.ObservationId,
                   CAST(STRING_AGG(CAST(i.EntityTypeId * 1000000 + i.EntityId AS VARCHAR(12)), ',')
                        WITHIN GROUP (ORDER BY i.EntityTypeId, i.EntityId) AS NVARCHAR(4000))
            FROM dbo.ObservationEntityIndex i
            JOIN dbo.Observation o ON o.Id = i.ObservationId
            JOIN dbo.Location l ON l.Id = o.LocationId
            WHERE l.GeometryTypeId IN (2,3,4,5) AND i.EntityTypeId IN (1,2,3,4,6)
            GROUP BY i.ObservationId;

            -- Boettene nummereres her, av signaturene selv. Se LesBoetter for
            -- hvorfor de ikke laanes fra AreaCountCacheBucketMember.
            SELECT Signatur, CAST(DENSE_RANK() OVER (ORDER BY Signatur) AS INT) AS BucketId
            INTO #gb FROM (SELECT DISTINCT Signatur FROM #sig) q;

            SELECT s.ObservationId, b.BucketId INTO #geo FROM #sig s JOIN #gb b ON b.Signatur = s.Signatur;
            CREATE CLUSTERED INDEX ix ON #geo(ObservationId);

            CREATE TABLE #psig (ObservationId INT PRIMARY KEY, Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT);
            INSERT #psig
            SELECT pr.ObservationId,
                   CAST(STRING_AGG(CAST(pr.ProjectOrgId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY pr.ProjectOrgId) AS NVARCHAR(4000))
            FROM dbo.ObservationProject pr
            JOIN dbo.Observation o ON o.Id = pr.ObservationId
            JOIN dbo.Location l ON l.Id = o.LocationId
            WHERE l.GeometryTypeId IN (2,3,4,5)
            GROUP BY pr.ObservationId;

            SELECT Signatur, CAST(DENSE_RANK() OVER (ORDER BY Signatur) AS INT) AS BucketId
            INTO #pb FROM (SELECT DISTINCT Signatur FROM #psig) q;

            SELECT s.ObservationId, b.BucketId INTO #proj FROM #psig s JOIN #pb b ON b.Signatur = s.Signatur;
            CREATE CLUSTERED INDEX ix ON #proj(ObservationId);
            """;

        // Boettemedlemskapet leses tilbake fra de samme signaturene.
        const string boetter = """
            SELECT b.BucketId, CAST(v.value AS INT)
            FROM #gb b CROSS APPLY STRING_SPLIT(b.Signatur, ',') v;

            SELECT b.BucketId, CAST(v.value AS INT)
            FROM #pb b CROSS APPLY STRING_SPLIT(b.Signatur, ',') v;
            """;

        var antall = await TellAsync(context,
            "SELECT COUNT_BIG(*) FROM dbo.Observation o JOIN dbo.Location l ON l.Id = o.LocationId WHERE l.GeometryTypeId IN (2,3,4,5)",
            ct);

        var n = (int)antall;
        _obsId = new int[n]; _regStatus = new byte[n]; _behavior = new byte[n];
        _harBilder = new bool[n]; _funntype = new int[n]; _kategori = new int[n];
        _institusjon = new int[n]; _taksongruppe = new int[n]; _datasett = new int[n];
        _koordpresisjon = new int[n]; _samletTicks = new long[n]; _maaned = new byte[n];
        _taksonIdx = new int[n]; _geoBucketIdx = new int[n]; _projBucketIdx = new int[n];

        var teller = new int[_locId.Length + 1];
        var lokPerObs = new int[n];

        await using var conn = NyTilkobling(context);
        await conn.OpenAsync(ct);

        await using (var forb = conn.CreateCommand())
        {
            forb.CommandText = forberedelse;
            forb.CommandTimeout = 0;
            await forb.ExecuteNonQueryAsync(ct);
        }

        await using (var bcmd = conn.CreateCommand())
        {
            bcmd.CommandText = boetter;
            bcmd.CommandTimeout = 0;
            await using var bleser = await bcmd.ExecuteReaderAsync(ct);
            LesBoetter(bleser, geografi: true);
            await bleser.NextResultAsync(ct);
            LesBoetter(bleser, geografi: false);
        }

        var i = 0;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.CommandTimeout = 0;
            await using var leser = await cmd.ExecuteReaderAsync(ct);
            while (await leser.ReadAsync(ct))
            {
                var locId = leser.GetInt32(1);
                if (!lokIndeks.TryGetValue(locId, out var loc)) continue;

                lokPerObs[i] = loc;
                teller[loc + 1]++;

                _obsId[i] = leser.GetInt32(0);
                _taksonIdx[i] = !leser.IsDBNull(2) && taksonIdx.TryGetValue(leser.GetInt32(2), out var t) ? t : Ingen;
                _taksongruppe[i] = leser.GetInt32(3);
                _kategori[i] = leser.IsDBNull(4) ? Ingen : leser.GetInt32(4);
                _funntype[i] = leser.GetInt32(5);
                _institusjon[i] = leser.IsDBNull(6) ? Ingen : leser.GetInt32(6);
                _datasett[i] = leser.IsDBNull(7) ? Ingen : leser.GetInt32(7);
                _regStatus[i] = leser.GetByte(8);
                _behavior[i] = leser.IsDBNull(9) ? (byte)0 : leser.GetByte(9);
                _harBilder[i] = leser.GetBoolean(10);
                _koordpresisjon[i] = leser.IsDBNull(11) ? Ingen : leser.GetInt32(11);
                _samletTicks[i] = leser.IsDBNull(12) ? long.MinValue : leser.GetDateTime(12).Ticks;
                _maaned[i] = leser.IsDBNull(13) ? (byte)0 : (byte)leser.GetInt32(13);
                _geoBucketIdx[i] = leser.IsDBNull(14) ? Ingen : _geoTett[leser.GetInt32(14)];
                _projBucketIdx[i] = leser.IsDBNull(15) ? Ingen : _projTett[leser.GetInt32(15)];
                i++;
            }
        }

        // Radene kom sortert på LocationId, så offsettene er bare en
        // kumulativ sum — ingen omstokking trengs.
        _obsStart = new int[_locId.Length + 1];
        for (var k = 1; k <= _locId.Length; k++) _obsStart[k] = _obsStart[k - 1] + teller[k];

        if (i != n)
        {
            Array.Resize(ref _obsId, i); Array.Resize(ref _regStatus, i); Array.Resize(ref _behavior, i);
            Array.Resize(ref _harBilder, i); Array.Resize(ref _funntype, i); Array.Resize(ref _kategori, i);
            Array.Resize(ref _institusjon, i); Array.Resize(ref _taksongruppe, i); Array.Resize(ref _datasett, i);
            Array.Resize(ref _koordpresisjon, i); Array.Resize(ref _samletTicks, i); Array.Resize(ref _maaned, i);
            Array.Resize(ref _taksonIdx, i); Array.Resize(ref _geoBucketIdx, i); Array.Resize(ref _projBucketIdx, i);
        }
    }

    private static SqlConnection NyTilkobling(IArtsKartDbContext context) =>
        new(((DbContext)context).Database.GetConnectionString());

    private static async Task<long> TellAsync(IArtsKartDbContext context, string sql, CancellationToken ct)
    {
        await using var conn = NyTilkobling(context);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
