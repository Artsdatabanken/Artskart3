using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Persistence.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kontrakten for områdebufferen: et oppslag skal gi nøyaktig det tellingen ville gitt,
/// celle for celle — eller la være å svare.
///
/// Testene sammenligner derfor alltid to repositories mot hverandre, ett med buffer og
/// ett uten, på samme filter. En buffer som svarer «nesten riktig» er verre enn ingen
/// buffer, så det holder ikke å sjekke at tallene ser rimelige ut.
///
/// Bufferen bygges i testen fra hele ObservationEntityIndex, slik byggejobben skal
/// gjøre det. Ville testen bygget bare sine egne rader, ville den ikke fanget at
/// oppslaget leser feil bøtte — de andre cellene ville manglet i begge svarene.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class AreaCountCacheIntegrationTests : IAsyncLifetime
{
    // EKTE områdetype-id, ikke en syntetisk. Tellestien slår opp
    // ObservationIndexEntityType.Municipality (= 1) i semi-joinen, så en syntetisk type
    // ville fått bufferen og tellingen til å se forskjellige rader — og testen ville
    // sammenlignet to ulike spørringer i stedet for to stier til samme svar.
    private const int AreaTypeMunicipalityId = AreaCountCacheDimensions.AreaTypeMunicipality;

    // Egen id-rekkevidde, klar av 97xxxx og 981xxx som de andre testene bruker.
    private const int ObservationBaseId = 982000;
    private const int AreaEntityOneId = 982010;
    private const int AreaEntityTwoId = 982011;
    private const string AreaFidOne = "982010";
    private const string AreaFidTwo = "982011";

    private const int RestrictedAreaOneId = 982100;
    private const int RestrictedAreaTwoId = 982101;

    // Havområde. Overlapper med kommune A, slik at «kommune + havområde» faktisk har
    // observasjoner i begge — ellers ville testen bestått uten å bevise noe.
    private const int OceanAreaId = 982400;
    private const string OceanFid = "982400";

    private const int TaxonGroupA = 982200;
    private const int TaxonGroupB = 982201;
    private const int CategoryA = 982300;
    private const int CategoryB = 982301;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    public AreaCountCacheIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await ClearCacheAsync();
        await _context.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Ettnivå
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Ettniva_gir_samme_tall_som_telling()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] });
    }

    [Fact]
    public async Task Ettniva_summerer_over_flere_valgte_verdier()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        // To verdier i samme dimensjon: hver observasjon har nøyaktig én taksongruppe,
        // så summen over bøttene teller hver rad én gang.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA, TaxonGroupB] });
    }

    [Fact]
    public async Task Ettniva_uten_treff_gir_tomt_ikke_ufiltrert()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        // GetAreaCountsAsync fjerner områder med antall 0, så «ingen treff» blir en tom
        // liste. Det viktige er at den er tom og ikke full — en bom skal aldri gi
        // ufiltrerte tall.
        var counts = await WithCache().GetAreaCountsAsync(2,
            new LocationSearchFilterDto { TaxonGroupIds = [999999] });

        counts.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Toernivå
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Toerniva_gir_samme_tall_som_telling()
    {
        await BuildLevel2Async(TaxonGroupDimensionId, CategoryDimensionId,
            r => r.TaxonGroupId, r => r.CategoryId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaxonGroupA],
            CategoryIds = [CategoryA],
        });
    }

    [Fact]
    public async Task Toerniva_er_uavhengig_av_rekkefolgen_filtrene_kommer_i()
    {
        await BuildLevel2Async(TaxonGroupDimensionId, CategoryDimensionId,
            r => r.TaxonGroupId, r => r.CategoryId);

        // Pargruppe-id-en sorterer på dimensjons-id, så BucketA hører alltid til den
        // laveste. Filteret har ingen rekkefølge, men oppslaget må ikke ha det heller.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            CategoryIds = [CategoryA, CategoryB],
            TaxonGroupIds = [TaxonGroupB],
        });
    }

    [Fact]
    public async Task Toerniva_brukes_ikke_naar_det_er_slaatt_av()
    {
        await BuildLevel2Async(TaxonGroupDimensionId, CategoryDimensionId,
            r => r.TaxonGroupId, r => r.CategoryId);

        var service = CreateCacheService(new AreaCountCacheOptions { EnableLevel2 = false });
        var result = await service.TryGetCountsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA], CategoryIds = [CategoryA] },
            await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Geografi — den viktigste delen av fila
    //
    // Kommune, fylke, Svalbard, verneområde og havområde er ÉN dimensjon, fordi
    // semi-joinen ORer dem sammen. Bøtta er hele mengden observasjonen tilhører, så
    // en observasjon telles én gang uansett hvor mange av de valgte områdene den
    // ligger i. Det er dette som gjør 100 %-overlappene ufarlige:
    // kommune mot fylke, og havområde mot Svalbard.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verneomraade_teller_overlappende_observasjon_en_gang()
    {
        await BuildGeographyCacheAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            RestrictedAreaIds = [RestrictedFid(RestrictedAreaOneId), RestrictedFid(RestrictedAreaTwoId)],
        });
    }

    [Fact]
    public async Task Verneomraade_med_ett_valgt_omraade()
    {
        await BuildGeographyCacheAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            RestrictedAreaIds = [RestrictedFid(RestrictedAreaOneId)],
        });
    }

    [Fact]
    public async Task Kommune_alene()
    {
        await BuildGeographyCacheAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { MunicipalityIds = [AreaFidOne] });
    }

    [Fact]
    public async Task Kommune_og_verneomraade_samtidig()
    {
        await BuildGeographyCacheAsync();

        // To områdetyper i ett utvalg. Observasjonene som ligger i begge skal telles
        // én gang — det er nettopp dette en summering per område ville fått galt.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [AreaFidOne],
            RestrictedAreaIds = [RestrictedFid(RestrictedAreaOneId)],
        });
    }

    [Fact]
    public async Task Kommune_og_havomraade_samtidig()
    {
        await BuildGeographyCacheAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [AreaFidOne],
            OceanAreaIds = [OceanFid],
        });
    }

    [Fact]
    public async Task Geografi_kombinert_med_et_annet_filter()
    {
        await BuildGeographyLevel2Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        // Toernivå med geografi. Det var dette tilfellet en splittet geografi-dimensjon
        // ikke kunne svart på uten et tredje bøttenivå.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [AreaFidOne],
            OceanAreaIds = [OceanFid],
            TaxonGroupIds = [TaxonGroupA],
        });
    }

    [Fact]
    public async Task Ukjent_omraade_gir_tomt()
    {
        await BuildGeographyCacheAsync();

        var service = CreateCacheService();
        var result = await service.TryGetCountsAsync(
            new LocationSearchFilterDto { RestrictedAreaIds = ["Naturbase VV00999999"] },
            await AreasAsync(), areasAreNarrowed: false);

        result.Should().NotBeNull().And.BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Når bufferen ikke skal svare
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Ubygget_buffer_gir_null()
    {
        await ClearCacheAsync();

        var result = await CreateCacheService().TryGetCountsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Feil_skjemaversjon_gir_null()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);
        await SetSchemaVersionAsync(AreaCountCacheDimensions.SchemaVersion + 1);

        var result = await CreateCacheService().TryGetCountsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull("en buffer bygget med et annet register svarer på forskjøvne pargruppe-id-er");
    }

    [Fact]
    public async Task Status_som_ikke_er_Ready_gir_null()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);
        await SetStatusAsync("Building");

        var result = await CreateCacheService().TryGetCountsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Tre_dimensjoner_gir_null()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        var result = await CreateCacheService().TryGetCountsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaxonGroupA],
            CategoryIds = [CategoryA],
            WithImages = true,
        }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Katalognummer_gir_null()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        var result = await CreateCacheService().TryGetCountsAsync(
            new LocationSearchFilterDto { ObservationIds = [ObservationBaseId] }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull("katalognummer har ikke noe endelig vokabular å bøtte på");
    }

    [Fact]
    public async Task Avslaatt_buffer_gir_null()
    {
        await BuildLevel1Async(TaxonGroupDimensionId, r => r.TaxonGroupId);

        var service = CreateCacheService(new AreaCountCacheOptions { Enabled = false });
        var result = await service.TryGetCountsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] }, await AreasAsync(), areasAreNarrowed: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_faller_tilbake_til_telling_naar_bufferen_ikke_svarer()
    {
        await ClearCacheAsync();

        // Uten buffer skal tallene være uendret. Det er hele grunnen til at
        // migrasjonen kan deployes før byggejobben finnes.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaxonGroupA] });
    }

    // -----------------------------------------------------------------------
    // Oppsett
    // -----------------------------------------------------------------------

    private static byte TaxonGroupDimensionId =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Taksongruppe").Id;

    private static byte CategoryDimensionId =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Kategori").Id;

    private static byte GeographyDimensionId =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Geografi").Id;

    private static string RestrictedFid(int entityId) => $"Naturbase VV{entityId:D8}";

    private SearchRepository WithCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(), CreateCacheService());

    private SearchRepository WithoutCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService());

    private AreaCountCacheService CreateCacheService(AreaCountCacheOptions? options = null) =>
        new(_context, new StubTaxonHierarchyService(), new StubAreaHierarchyService(),
            Options.Create(options ?? new AreaCountCacheOptions()),
            NullLogger<AreaCountCacheService>.Instance);

    private Task<List<Area>> AreasAsync() =>
        _context.Set<Area>().AsNoTracking().Where(a => a.IsCurrent && a.ZoomLevel == 2).ToListAsync();

    /// <summary>
    /// Kjører samme filter med og uten buffer og krever identiske tall for hvert
    /// område. Sammenligner på Fid, ikke bare totalen — to svar kan ha samme sum og
    /// likevel fordele den ulikt.
    /// </summary>
    private async Task AssertSameAsCountingAsync(LocationSearchFilterDto filter)
    {
        var medBuffer = await WithCache().GetAreaCountsAsync(2, filter);
        var utenBuffer = await WithoutCache().GetAreaCountsAsync(2, filter);

        medBuffer.Select(a => (a.Fid, a.ObservationCount)).OrderBy(x => x.Fid)
            .Should().Equal(utenBuffer.Select(a => (a.Fid, a.ObservationCount)).OrderBy(x => x.Fid));
    }

    private async Task ClearCacheAsync()
    {
        await _context.Set<AreaCountCacheLevel1>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheLevel2>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheBucketMember>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheState>().ExecuteDeleteAsync();
    }

    private async Task MarkReadyAsync()
    {
        _context.Set<AreaCountCacheState>().Add(new AreaCountCacheState
        {
            Id = 1,
            SchemaVersion = AreaCountCacheDimensions.SchemaVersion,
            Status = "Ready",
            BuiltAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private Task SetStatusAsync(string status) =>
        _context.Set<AreaCountCacheState>().Where(s => s.Id == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));

    private Task SetSchemaVersionAsync(int version) =>
        _context.Set<AreaCountCacheState>().Where(s => s.Id == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SchemaVersion, version));

    /// <summary>
    /// Bygger ettnivå fra HELE indekstabellen, slik byggejobben skal gjøre det.
    /// Bøtter bare sine egne rader, og de andre cellene ville manglet i begge svar —
    /// da hadde sammenligningen bestått uten å bevise noe.
    /// </summary>
    private async Task BuildLevel1Async(byte dimensionId, Func<ObservationEntityIndex, int?> bucketOf)
    {
        await ClearCacheAsync();

        var rows = await _context.Set<ObservationEntityIndex>().AsNoTracking().ToListAsync();

        _context.Set<AreaCountCacheLevel1>().AddRange(rows
            .GroupBy(r => (Bucket: bucketOf(r) ?? AreaCountCacheDimensions.NullBucket, r.EntityTypeId, r.EntityId))
            .Select(g => new AreaCountCacheLevel1
            {
                DimensionId = dimensionId,
                BucketId = g.Key.Bucket,
                EntityTypeId = g.Key.EntityTypeId,
                EntityId = g.Key.EntityId,
                // Rader, ikke distinkte observasjoner — samme som g.Count() i
                // ComputeFilteredAreaCounts.
                ObservationCount = g.Count(),
            }));

        await MarkReadyAsync();
    }

    private async Task BuildLevel2Async(
        byte dimensionA, byte dimensionB,
        Func<ObservationEntityIndex, int?> bucketA, Func<ObservationEntityIndex, int?> bucketB)
    {
        await ClearCacheAsync();

        var rows = await _context.Set<ObservationEntityIndex>().AsNoTracking().ToListAsync();
        var pairId = AreaCountCacheDimensions.GetPairId(dimensionA, dimensionB);

        // BucketA hører til dimensjonen med LAVEST id, ikke til det første argumentet.
        // Pargruppe-id-en sorterer, så oppslaget forventer den rekkefølgen — og en
        // byggejobb som bruker argumentrekkefølgen ville lagt tallene i feil kolonne.
        if (dimensionA > dimensionB)
            (bucketA, bucketB) = (bucketB, bucketA);

        _context.Set<AreaCountCacheLevel2>().AddRange(rows
            .GroupBy(r => (
                A: bucketA(r) ?? AreaCountCacheDimensions.NullBucket,
                B: bucketB(r) ?? AreaCountCacheDimensions.NullBucket,
                r.EntityTypeId, r.EntityId))
            .Select(g => new AreaCountCacheLevel2
            {
                DimensionPairId = pairId,
                BucketA = g.Key.A,
                BucketB = g.Key.B,
                EntityTypeId = g.Key.EntityTypeId,
                EntityId = g.Key.EntityId,
                ObservationCount = g.Count(),
            }));

        await MarkReadyAsync();
    }

    /// <summary>
    /// Bygger verneområdedimensjonen: bøtta er hele mengden verneområder en observasjon
    /// ligger i, og medlemstabellen sier hvilke bøtter et gitt område inngår i.
    ///
    /// Observasjoner uten verneområde havner i NULL-bøtta. De kan aldri velges av et
    /// verneområdefilter, men må være med for at bøttene skal dekke alle rader.
    /// </summary>
    /// <summary>
    /// Områdetypene geografi-dimensjonen dekker. Speiler semi-joinen i
    /// ComputeFilteredAreaCounts — kommune, fylke, verneområde, havområde, Svalbard.
    /// Testene bruker en syntetisk kommunetype, så den byttes inn for type 1.
    /// </summary>
    private static readonly int[] GeographyTypes =
    [
        AreaTypeMunicipalityId,
        AreaCountCacheDimensions.AreaTypeCounty,
        AreaCountCacheDimensions.AreaTypeRestricted,
        AreaCountCacheDimensions.AreaTypeOcean,
        AreaCountCacheDimensions.AreaTypeSvalbard,
    ];

    /// <summary>
    /// Bygger geografi-dimensjonen: bøtta er hele mengden områder en observasjon
    /// tilhører, på tvers av områdetyper, og medlems-id-en pakker typen inn.
    /// </summary>
    private Dictionary<int, int> BuildGeographyBuckets(List<ObservationEntityIndex> rows)
    {
        var membership = rows
            .Where(r => GeographyTypes.Contains(r.EntityTypeId))
            .GroupBy(r => r.ObservationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => PackMember(r.EntityTypeId, r.EntityId))
                      .Distinct().OrderBy(m => m).ToArray());

        var bucketByKey = membership.Values
            .Select(members => string.Join(',', members))
            .Distinct()
            .Select((key, index) => (key, bucket: index + 1))
            .ToDictionary(x => x.key, x => x.bucket);

        _context.Set<AreaCountCacheBucketMember>().AddRange(bucketByKey
            .SelectMany(kv => kv.Key.Split(',').Select(member => new AreaCountCacheBucketMember
            {
                DimensionId = GeographyDimensionId,
                MemberId = int.Parse(member),
                BucketId = kv.Value,
            })));

        return membership.ToDictionary(
            kv => kv.Key,
            kv => bucketByKey[string.Join(',', kv.Value)]);
    }

    private static int PackMember(int entityTypeId, int entityId) =>
        AreaCountCacheDimensions.PackAreaMember(entityTypeId, entityId);

    private async Task BuildGeographyCacheAsync()
    {
        await ClearCacheAsync();

        var rows = await _context.Set<ObservationEntityIndex>().AsNoTracking().ToListAsync();
        var bucketOf = BuildGeographyBuckets(rows);

        _context.Set<AreaCountCacheLevel1>().AddRange(rows
            .GroupBy(r => (
                Bucket: bucketOf.TryGetValue(r.ObservationId, out var b) ? b : AreaCountCacheDimensions.NullBucket,
                r.EntityTypeId, r.EntityId))
            .Select(g => new AreaCountCacheLevel1
            {
                DimensionId = GeographyDimensionId,
                BucketId = g.Key.Bucket,
                EntityTypeId = g.Key.EntityTypeId,
                EntityId = g.Key.EntityId,
                ObservationCount = g.Count(),
            }));

        await MarkReadyAsync();
    }

    /// <summary>Toernivå med geografi som den ene dimensjonen.</summary>
    private async Task BuildGeographyLevel2Async(byte otherDimension, Func<ObservationEntityIndex, int?> otherBucket)
    {
        await ClearCacheAsync();

        var rows = await _context.Set<ObservationEntityIndex>().AsNoTracking().ToListAsync();
        var bucketOf = BuildGeographyBuckets(rows);
        var pairId = AreaCountCacheDimensions.GetPairId(GeographyDimensionId, otherDimension);

        // BucketA hører til dimensjonen med lavest id.
        var geoIsA = GeographyDimensionId < otherDimension;

        _context.Set<AreaCountCacheLevel2>().AddRange(rows
            .GroupBy(r => (
                Geo: bucketOf.TryGetValue(r.ObservationId, out var b) ? b : AreaCountCacheDimensions.NullBucket,
                Other: otherBucket(r) ?? AreaCountCacheDimensions.NullBucket,
                r.EntityTypeId, r.EntityId))
            .Select(g => new AreaCountCacheLevel2
            {
                DimensionPairId = pairId,
                BucketA = geoIsA ? g.Key.Geo : g.Key.Other,
                BucketB = geoIsA ? g.Key.Other : g.Key.Geo,
                EntityTypeId = g.Key.EntityTypeId,
                EntityId = g.Key.EntityId,
                ObservationCount = g.Count(),
            }));

        await MarkReadyAsync();
    }

    private async Task SeedAsync()
    {
        if (await _context.Set<Area>().AnyAsync(a => a.Fid == AreaFidOne))
            return;

        // AreaType 1 (Municipality) kommer fra seed_data.sql og skal ikke opprettes her.

        _context.Set<Area>().AddRange(
            NewArea(AreaFidOne, "Buffer kommune A"),
            NewArea(AreaFidTwo, "Buffer kommune B"));

        // Seks observasjoner fordelt på to kommuner, to taksongrupper og to kategorier,
        // slik at både ett- og toernivå får mer enn én celle å fordele på.
        //
        // De to første ligger dessuten i verneområder: 0 i begge, 1 i det første.
        // Det gir nettopp overlappet som en naiv summering per område ville dobbelttalt.
        for (var i = 0; i < 6; i++)
        {
            var observationId = ObservationBaseId + i;
            var areaEntityId = i < 3 ? AreaEntityOneId : AreaEntityTwoId;

            _context.Set<ObservationEntityIndex>().Add(NewIndexRow(
                observationId, AreaTypeMunicipalityId, areaEntityId,
                i % 2 == 0 ? TaxonGroupA : TaxonGroupB,
                i < 4 ? CategoryA : CategoryB));
        }

        // Observasjon 0 ligger i BEGGE verneområdene — det er dette overlappet en
        // summering per område ville dobbelttalt.
        AddAreaRow(ObservationBaseId, AreaCountCacheDimensions.AreaTypeRestricted, RestrictedAreaOneId, TaxonGroupA, CategoryA);
        AddAreaRow(ObservationBaseId, AreaCountCacheDimensions.AreaTypeRestricted, RestrictedAreaTwoId, TaxonGroupA, CategoryA);
        AddAreaRow(ObservationBaseId + 1, AreaCountCacheDimensions.AreaTypeRestricted, RestrictedAreaOneId, TaxonGroupB, CategoryA);

        // Observasjon 0 og 2 ligger også i havområdet, altså i både kommune A og hav.
        // Det speiler kystobservasjonene med grov koordinatpresisjon i produksjon.
        AddAreaRow(ObservationBaseId, AreaCountCacheDimensions.AreaTypeOcean, OceanAreaId, TaxonGroupA, CategoryA);
        AddAreaRow(ObservationBaseId + 2, AreaCountCacheDimensions.AreaTypeOcean, OceanAreaId, TaxonGroupA, CategoryA);

        await _context.SaveChangesAsync();
    }

    private void AddAreaRow(int observationId, int entityTypeId, int entityId, int taxonGroupId, int categoryId) =>
        _context.Set<ObservationEntityIndex>().Add(
            NewIndexRow(observationId, entityTypeId, entityId, taxonGroupId, categoryId));

    private static ObservationEntityIndex NewIndexRow(
        int observationId, int entityTypeId, int entityId, int taxonGroupId, int categoryId) => new()
        {
            ObservationId = observationId,
            EntityTypeId = entityTypeId,
            EntityId = entityId,
            TaxonGroupId = taxonGroupId,
            CategoryId = categoryId,
            BasisOfRecordId = 920001,
            RegistrationStatusId = 1,
        };

    private static Area NewArea(string fid, string name) => new()
    {
        DocumentId = Guid.NewGuid().ToString("N"),
        Fid = fid,
        Name = name,
        AreaTypeId = AreaTypeMunicipalityId,
        ZoomLevel = 2,
        ParentFid = "buffer-parent",
        SyncDateTime = DateTime.UtcNow,
        ObservationCount = 0,
        Bbox = "bbox",
        TimeStamp = DateTime.UtcNow,
        IsCurrent = true,
    };
}
