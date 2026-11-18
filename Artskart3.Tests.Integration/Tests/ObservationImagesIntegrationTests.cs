using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Entities = Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Galleriet henter de nyeste funnene med mediefiler først og bildene deretter.
///
/// Det hviler på at hvert funn med HasMediaFiles har minst ett bilde. Det stemmer
/// ikke alltid — lyd, dokumenter og slettede filer teller også — så de fire nyeste
/// funnene her har bare et dokument. Uten etterhentingen ville siden blitt tom.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class ObservationImagesIntegrationTests : IAsyncLifetime
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Verneomraade = (int)ObservationIndexEntityType.RestrictedArea;

    // Egen id-rekkevidde, klar av de andre testklassene.
    private const int KommuneA = 984610;
    private const int KommuneB = 984611;
    private const int KommuneC = 984612;
    private const int VerneA = 984640;
    private const string KommuneAFid = "984610";
    private const string KommuneBFid = "984611";
    private const string KommuneCFid = "984612";
    private const string VerneAFid = "Naturbase VV984640";

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _lokasjon = null!;
    private MediaFileType _bilde = null!;
    private MediaFileType _dokument = null!;
    private List<Observation> _funn = [];

    // Bildene i forventet rekkefølge: nyeste funn først, deretter bilde-id.
    private int[] _bilderA = [];
    private int _bildeB;
    private int _bildeVerne;
    private int _bildeC;

    public ObservationImagesIntegrationTests(DatabaseFixture db) => _db = db;

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
        var ids = _funn.Select(o => o.Id).ToList();
        await _context.Set<MediaFile>().Where(m => m.ObservationId != null && ids.Contains(m.ObservationId.Value)).ExecuteDeleteAsync();
        await _context.Set<ObservationEntityIndex>().Where(i => ids.Contains(i.ObservationId)).ExecuteDeleteAsync();
        await _context.Set<Observation>().Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync();
        await _context.Set<MediaFileType>().Where(t => t.Id == _bilde.Id || t.Id == _dokument.Id).ExecuteDeleteAsync();
        await _context.DisposeAsync();
    }

    private SearchRepository Repository() =>
        new(_context, NullLogger<SearchRepository>.Instance,
            Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

    // -----------------------------------------------------------------------

    /// <summary>
    /// Én side à 1 gir take 4. De fire nyeste funnene i kommune A har bare et
    /// dokument, så første runde finner ingen bilder og må hente dobbelt så mange.
    /// </summary>
    [Fact]
    public async Task Ett_omraade_henter_flere_funn_naar_de_nyeste_ikke_har_bilder()
    {
        var bilder = await Repository().GetObservationImagesAsync(Filter(1, 1, municipalityIds: [KommuneAFid]));

        bilder.Select(b => b.Image.Id).Should().Equal(_bilderA);
    }

    /// <summary>
    /// To områder går fortsatt per område. Side 2 hopper over ett BILDE, ikke ett
    /// funn — det første funnet med bilder har to, så side 2 starter midt i det.
    /// </summary>
    [Fact]
    public async Task To_omraader_sorterer_og_blar_paa_tvers_av_omraadene()
    {
        var side1 = await Repository().GetObservationImagesAsync(Filter(1, 1, municipalityIds: [KommuneAFid, KommuneBFid]));
        var side2 = await Repository().GetObservationImagesAsync(Filter(2, 1, municipalityIds: [KommuneAFid, KommuneBFid]));

        int[] alle = [_bilderA[0], _bilderA[1], _bildeB, _bilderA[2]];
        side1.Select(b => b.Image.Id).Should().Equal(alle);
        side2.Select(b => b.Image.Id).Should().Equal(alle.Skip(1));
    }

    /// <summary>
    /// Over taket brukes den generelle spørringen mot Observation, som må gi samme
    /// svar som områdestien ville gitt.
    /// </summary>
    [Fact]
    public async Task Flere_enn_to_omraader_gir_samme_svar_med_den_generelle_spoerringen()
    {
        var bilder = await Repository().GetObservationImagesAsync(
            Filter(1, 2, municipalityIds: [KommuneAFid, KommuneBFid], restrictedAreaIds: [VerneAFid]));

        bilder.Select(b => b.Image.Id).Should().Equal(_bilderA[0], _bilderA[1], _bildeB, _bilderA[2], _bildeVerne);
    }

    /// <summary>
    /// Kommune C har flere nyere funn uten bilder enn etterhentingen rekker gjennom.
    /// Galleriet skal likevel finne bildet, ikke melde at filteret er tomt.
    /// </summary>
    [Fact]
    public async Task Gir_ikke_opp_naar_for_mange_nyere_funn_mangler_bilder()
    {
        var bilder = await Repository().GetObservationImagesAsync(Filter(1, 1, municipalityIds: [KommuneCFid]));

        bilder.Select(b => b.Image.Id).Should().Equal(_bildeC);
    }

    /// <summary>
    /// Større sider enn ID-taket pagineres i databasen, med samme rekkefølge.
    /// </summary>
    [Fact]
    public async Task Sider_over_id_taket_gir_samme_svar()
    {
        var bilder = await Repository().GetObservationImagesAsync(
            Filter(1, 10_000, municipalityIds: [KommuneAFid, KommuneBFid]));

        bilder.Select(b => b.Image.Id).Should().Equal(_bilderA[0], _bilderA[1], _bildeB, _bilderA[2]);
    }

    // -----------------------------------------------------------------------

    private static ObservationSearchFilterDto Filter(
        int pageNumber, int resultsPerPage, string[]? municipalityIds = null, string[]? restrictedAreaIds = null) => new()
    {
        PageNumber = pageNumber,
        ResultsPerPage = resultsPerPage,
        MunicipalityIds = municipalityIds,
        RestrictedAreaIds = restrictedAreaIds,
    };

    private async Task SeedAsync()
    {
        _lokasjon = await EnsureLocationAsync();
        _bilde = new MediaFileType { Id = 984660, MediaTypeName = "Image", MimeType = "image/jpeg" };
        _dokument = new MediaFileType { Id = 984661, MediaTypeName = "Document", MimeType = "application/pdf" };
        _context.Set<MediaFileType>().AddRange(_bilde, _dokument);

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();
        Observation Funn(int dag, bool harMedia)
        {
            var funn = NewObservation(_lokasjon, taxon, taxonName, kategori, funntype);
            funn.DateTimeCollected = new DateTime(2001, 1, dag);
            funn.HasMediaFiles = harMedia;
            return funn;
        }

        // Nyeste først. Dokumentfunnene og funnet uten mediefiler ligger foran alle bildene.
        var dokumentfunn = Enumerable.Range(0, 4).Select(i => Funn(20 - i, harMedia: true)).ToList();
        var utenMedia = Funn(15, harMedia: false);
        var toBilderA = Funn(14, harMedia: true);
        var bildeB = Funn(13, harMedia: true);
        var bildeA = Funn(12, harMedia: true);
        var bildeVerne = Funn(11, harMedia: true);
        var dokumentfunnC = Enumerable.Range(0, 20).Select(i => Funn(31 - i, harMedia: true)).ToList();
        var bildeC = Funn(1, harMedia: true);
        _funn = [.. dokumentfunn, utenMedia, toBilderA, bildeB, bildeA, bildeVerne, .. dokumentfunnC, bildeC];
        _context.Set<Observation>().AddRange(_funn);
        await _context.SaveChangesAsync();

        var media = dokumentfunn.Concat(dokumentfunnC).Select(o => Media(o, _dokument))
            .Append(Media(toBilderA, _bilde)).Append(Media(toBilderA, _bilde))
            .Append(Media(bildeB, _bilde)).Append(Media(bildeA, _bilde)).Append(Media(bildeVerne, _bilde))
            .Append(Media(bildeC, _bilde))
            .ToList();
        _context.Set<MediaFile>().AddRange(media);

        _context.Set<ObservationEntityIndex>().AddRange(
            dokumentfunn.Append(utenMedia).Append(toBilderA).Append(bildeA).Select(o => Rad(o, Kommune, KommuneA))
                .Append(Rad(bildeB, Kommune, KommuneB))
                .Append(Rad(bildeVerne, Verneomraade, VerneA))
                .Concat(dokumentfunnC.Append(bildeC).Select(o => Rad(o, Kommune, KommuneC))));
        await _context.SaveChangesAsync();

        int BildeId(Observation o, int nr) => media.Where(m => m.ObservationId == o.Id).OrderBy(m => m.Id).ElementAt(nr).Id;
        _bilderA = [BildeId(toBilderA, 0), BildeId(toBilderA, 1), BildeId(bildeA, 0)];
        _bildeB = BildeId(bildeB, 0);
        _bildeVerne = BildeId(bildeVerne, 0);
        _bildeC = BildeId(bildeC, 0);
    }

    private static MediaFile Media(Observation funn, MediaFileType type) => new()
    {
        ObservationId = funn.Id,
        Origin = $"https://example.org/{funn.Id}.jpg",
        MediaFileType = type,
        Downloaded = false,
    };

    private ObservationEntityIndex Rad(Observation funn, int entityTypeId, int entityId) => new()
    {
        ObservationId = funn.Id,
        EntityTypeId = entityTypeId,
        EntityId = entityId,
        LocationId = _lokasjon.Id,
        TaxonGroupId = funn.TaxonGroupId,
        CategoryId = funn.CategoryId,
        BasisOfRecordId = funn.BasisOfRecordId,
        RegistrationStatusId = 1,
        DateTimeCollected = funn.DateTimeCollected,
        HasMediaFiles = funn.HasMediaFiles,
    };

    private async Task<Location> EnsureLocationAsync()
    {
        const string lookupId = "gallery-loc-1";

        var funnet = await _context.Set<Location>().FirstOrDefaultAsync(l => l.LookupId == lookupId);
        if (funnet != null) return funnet;

        var ny = new Location
        {
            LookupId = lookupId,
            Latitude = 60.1,
            Longitude = 11.2,
            CoordinatePrecision = 25,
            East = 284_000,
            North = 6_671_000,
            Locality = lookupId,
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = null,
        };

        _context.Set<Location>().Add(ny);
        await _context.SaveChangesAsync();
        return ny;
    }

    private static Observation NewObservation(
        Location lokasjon, Taxon taxon, TaxonName taxonName,
        Entities.Category kategori, Entities.BasisOfRecord funntype) => new()
    {
        DateLastModified = DateTime.UtcNow,
        DateTimeRecordImported = DateTime.UtcNow,
        DateTimeRecordProcessed = DateTime.UtcNow,
        NodeId = 1,
        BasisOfRecordId = funntype.Id,
        TaxonId = taxon.Id,
        MatchedScientificNameId = taxonName.Id,
        TaxonGroupId = taxon.TaxonGroupId,
        CategoryId = kategori.Id,
        Latitude = lokasjon.Latitude ?? 0,
        Longitude = lokasjon.Longitude ?? 0,
        CoordinatePrecisionInMeters = 25,
        East = lokasjon.East,
        North = lokasjon.North,
        LocationId = lokasjon.Id,
        HashCode = 0,
        ProcessEngineId = 1,
        HasErrors = false,
        HasAnnotations = false,
    };
}
