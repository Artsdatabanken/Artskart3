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
/// Områdefilteret er forankret PÅ områderaden i stedet for slått opp som
/// søsterrad med EXISTS. Det halverte tiden, men flytter samtidig et
/// korrekthetsansvar: den forankrede formen gir én rad per (observasjon,
/// område), så en observasjon i flere valgte områder treffer flere ganger.
///
/// At den likevel teller riktig hviler helt på COUNT(DISTINCT ObservationId) i
/// kalleren. Blir den erstattet med COUNT(*) en dag, dobbelttelles hver
/// observasjon som ligger i mer enn ett valgt område — og det ville ikke gitt
/// noen feilmelding, bare for høye tall på kartet.
///
/// Derfor tester disse først og fremst OVERLAPP.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class AreaFilterAnchoringIntegrationTests : IAsyncLifetime
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;
    private const int Havomraade = (int)ObservationIndexEntityType.OceanArea;
    private const int Verneomraade = (int)ObservationIndexEntityType.RestrictedArea;

    // Egen id-rekkevidde, klar av 97xxxx, 98[123]xxx.
    private const int KommuneA = 984010;
    private const int KommuneB = 984011;
    private const int FylkeA = 984020;
    private const int HavA = 984030;
    private const int VerneA = 984040;

    private const string KommuneAFid = "984010";
    private const string KommuneBFid = "984011";
    private const string FylkeAFid = "984020";
    private const string HavAFid = "984030";
    private const string VerneAFid = "Naturbase VV984040";

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _lokasjon = null!;

    public AreaFilterAnchoringIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private SearchRepository Repository() =>
        new(_context, NullLogger<SearchRepository>.Instance,
            Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService());

    // -----------------------------------------------------------------------

    /// <summary>
    /// KJERNEN. Observasjon 1 ligger i kommune A, fylke A, havområde A og
    /// verneområde A — fire rader. Velges alle fire, treffer den fire ganger,
    /// men skal telles ÉN gang.
    /// </summary>
    [Fact]
    public async Task Observasjon_i_flere_valgte_omraader_telles_en_gang()
    {
        var result = await Repository().GetLocationsAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [KommuneAFid],
            CountyIds = [FylkeAFid],
            OceanAreaIds = [HavAFid],
            RestrictedAreaIds = [VerneAFid],
        });

        var lokasjon = result.Single(x => x.Id == _lokasjon.Id);

        // Alle tre observasjonene treffer, men på ulikt grunnlag:
        //   obs 0 — fire rader (kommune, fylke, hav, verneområde)
        //   obs 1 — én rad (kommune A)
        //   obs 2 — én rad (Svalbard med fylke A sin id, siden fylkesvalg
        //           også slår opp Svalbard)
        //
        // Seks treffende rader, tre observasjoner. Ville obs 0 blitt talt per
        // rad, sto det 6 her.
        lokasjon.ObservationCount.Should().Be(3,
            "obs 0 treffer fire ganger men skal telles én gang");
    }

    /// <summary>
    /// Samme observasjon, men bare ett område valgt. Fasit for testen over.
    /// </summary>
    [Fact]
    public async Task Ett_omraade_gir_forventet_antall()
    {
        var result = await Repository().GetLocationsAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [KommuneAFid],
        });

        result.Single(x => x.Id == _lokasjon.Id).ObservationCount.Should().Be(2);
    }

    /// <summary>
    /// To områder som IKKE overlapper. Da er summen additiv, og et filter som
    /// mistet rader ville vist seg her.
    /// </summary>
    [Fact]
    public async Task To_ikke_overlappende_omraader_summeres()
    {
        var result = await Repository().GetLocationsAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = [KommuneAFid, KommuneBFid],
        });

        result.Single(x => x.Id == _lokasjon.Id).ObservationCount.Should().Be(3);
    }

    /// <summary>
    /// Fylkes-IDer slås opp mot BÅDE fylke og Svalbard. Observasjon 3 har bare
    /// en Svalbard-rad med fylke A sin id, og skal være med.
    /// </summary>
    [Fact]
    public async Task Fylkesvalg_treffer_ogsaa_Svalbard()
    {
        var result = await Repository().GetLocationsAsync(new LocationSearchFilterDto
        {
            CountyIds = [FylkeAFid],
        });

        // Observasjon 1 via fylkesraden, observasjon 3 via Svalbard-raden.
        result.Single(x => x.Id == _lokasjon.Id).ObservationCount.Should().Be(2);
    }

    /// <summary>
    /// Et område uten treff skal gi tomt, ikke ufiltrerte tall. En forankring
    /// som falt tilbake til «alle rader» ville bestått alle testene over.
    /// </summary>
    [Fact]
    public async Task Ukjent_omraade_gir_tomt()
    {
        var result = await Repository().GetLocationsAsync(new LocationSearchFilterDto
        {
            MunicipalityIds = ["984999"],
        });

        result.Should().NotContain(x => x.Id == _lokasjon.Id);
    }

    // -----------------------------------------------------------------------

    private async Task SeedAsync()
    {
        _lokasjon = await EnsureLocationAsync();

        if (await _context.Set<ObservationEntityIndex>()
                .AnyAsync(i => i.EntityTypeId == Kommune && i.EntityId == KommuneA))
        {
            return;
        }

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();

        var obs = Enumerable.Range(0, 3)
            .Select(_ => NewObservation(_lokasjon, taxon, taxonName, kategori, funntype))
            .ToArray();

        _context.Set<Observation>().AddRange(obs);
        await _context.SaveChangesAsync();

        // Observasjon 0: i ALLE fire områdene — overlappstilfellet.
        // Observasjon 1: bare kommune A.
        // Observasjon 2: bare kommune B, pluss en Svalbard-rad med fylke A sin id.
        _context.Set<ObservationEntityIndex>().AddRange(
            Rad(obs[0].Id, Kommune, KommuneA),
            Rad(obs[0].Id, Fylke, FylkeA),
            Rad(obs[0].Id, Havomraade, HavA),
            Rad(obs[0].Id, Verneomraade, VerneA),
            Rad(obs[1].Id, Kommune, KommuneA),
            Rad(obs[2].Id, Kommune, KommuneB),
            Rad(obs[2].Id, (int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen, FylkeA));

        await _context.SaveChangesAsync();
    }

    private async Task<Location> EnsureLocationAsync()
    {
        const string lookupId = "anchor-loc-1";

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

    private ObservationEntityIndex Rad(int observationId, int entityTypeId, int entityId) => new()
    {
        ObservationId = observationId,
        EntityTypeId = entityTypeId,
        EntityId = entityId,
        LocationId = _lokasjon.Id,
        TaxonGroupId = 984200,
        CategoryId = 984300,
        BasisOfRecordId = 984400,
        RegistrationStatusId = 1,
    };

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
