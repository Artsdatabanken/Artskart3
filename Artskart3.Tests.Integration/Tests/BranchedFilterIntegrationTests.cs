using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Entities = Artskart3.Core.Domain.Entities;
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
/// Listevisningen deler flerverdifiltre på institusjon og taksongruppe i én
/// spørring per verdi og fletter i minnet, fordi IN over flere selektive
/// verdier tvinger fram en sortering av alle treffene. Målt på prodlik base:
/// 2520 ms med IN, 34 ms med grener.
///
/// Det er en ren ytelsesomskriving — svaret skal være identisk. Disse testene
/// vokter nøyaktig det, og særlig det ene stedet grening lett blir feil:
/// hver gren må hente skip+take rader, ikke take.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class BranchedFilterIntegrationTests : IAsyncLifetime
{
    /// <summary>
    /// Nok rader per institusjon til at skip+take-grensen faktisk kan brytes.
    /// Med resultsPerPage 5 og LookaheadMultiplier 4 er take 20, og side 3 gir
    /// skip 10 — altså 30 rader per gren. Færre enn 30 her, og testen under
    /// ville bestått selv med feil grense.
    /// </summary>
    private const int AntallPerInstitusjon = 40;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _lokasjon = null!;

    // Institusjon A har de nyeste radene, B de eldre. Rekkefølgen er med vilje
    // IKKE flettet: skal side 3 fylles, må A alene levere rad 11-30, og det er
    // bare mulig om grenen henter skip+take.
    private int _instA;
    private int _instB;
    private int[] _instMange = [];

    private int _taksongruppeA;
    private int _taksongruppeB;

    public BranchedFilterIntegrationTests(DatabaseFixture db) => _db = db;

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
    /// DEN VIKTIGSTE. Side 3 av et toverdifilter der ALLE radene på siden kommer
    /// fra én gren.
    ///
    /// Henter hver gren bare «take» rader, får vi A sine 20 nyeste og B sine 20
    /// nyeste. Etter skip 10 begynner svaret riktig, men rad 21-30 fra A finnes
    /// ikke i materialet, og B sine rader rykker opp i stedet. Antallet blir
    /// like riktig som før, og bare rekkefølgen avslører feilen — derfor
    /// sammenlignes ID-ene, ikke bare antallet.
    /// </summary>
    [Fact]
    public async Task Dyp_side_henter_rader_utover_forste_gren()
    {
        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            OrganizationIds = [_instA, _instB],
            PageNumber = 3,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(
            o => o.InstitutionOrgId == _instA || o.InstitutionOrgId == _instB,
            skip: 10, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
    }

    /// <summary>
    /// Side 1 av samme filter. Fasit for testen over: går denne i stas men den
    /// andre feiler, er det grensen og ikke flettingen som er gal.
    /// </summary>
    [Fact]
    public async Task Forste_side_er_datosortert_paa_tvers_av_grenene()
    {
        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            OrganizationIds = [_instA, _instB],
            PageNumber = 1,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(
            o => o.InstitutionOrgId == _instA || o.InstitutionOrgId == _instB,
            skip: 0, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
    }

    /// <summary>
    /// Én verdi skal ikke grenes, men gå rett gjennom. Verdien av testen er at
    /// den fanger en grenlogikk som ved et uhell begynner på 1 i stedet for 2 og
    /// dermed legger en runde med fletting på den vanligste spørringen av alle.
    /// </summary>
    [Fact]
    public async Task En_verdi_gir_samme_svar()
    {
        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            OrganizationIds = [_instA],
            PageNumber = 2,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(o => o.InstitutionOrgId == _instA, skip: 5, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
    }

    /// <summary>
    /// Over taket faller vi tilbake til IN-formen. Svaret skal være det samme —
    /// det er bare planen som er en annen. Ni verdier, taket er åtte.
    /// </summary>
    [Fact]
    public async Task Over_grensen_faller_tilbake_og_svarer_likt()
    {
        var alle = _instMange;
        alle.Should().HaveCountGreaterThan(8, "testen skal ligge over MaxBranches");

        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            OrganizationIds = alle,
            PageNumber = 1,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(
            o => o.InstitutionOrgId != null && alle.Contains(o.InstitutionOrgId.Value),
            skip: 0, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
    }

    /// <summary>
    /// Taksongruppe grenes på samme måte. Her er radene flettet mellom grenene,
    /// så en fletting som sorterte feil ville gitt utslag.
    /// </summary>
    [Fact]
    public async Task Taksongruppe_grenes_og_flettes_riktig()
    {
        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            TaxonGroupIds = [_taksongruppeA, _taksongruppeB],
            PageNumber = 2,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(
            o => o.TaxonGroupId == _taksongruppeA || o.TaxonGroupId == _taksongruppeB,
            skip: 5, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
    }

    /// <summary>
    /// Er begge dimensjonene flerverdi, grenes bare den ene og den andre blir et
    /// restledd inne i hver gren. Da må OG-en mellom dem fortsatt holde: et
    /// resultat som mistet restleddet ville fått med rader fra feil taksongruppe.
    /// </summary>
    [Fact]
    public async Task Begge_dimensjonene_flerverdi_gir_snittet()
    {
        var resultat = await Repository().GetObservationsAsync(new ObservationSearchFilterDto
        {
            OrganizationIds = [_instA, _instB],
            TaxonGroupIds = [_taksongruppeA, _taksongruppeB],
            PageNumber = 1,
            ResultsPerPage = 5,
        });

        var forventet = await ForventetAsync(
            o => (o.InstitutionOrgId == _instA || o.InstitutionOrgId == _instB)
                 && (o.TaxonGroupId == _taksongruppeA || o.TaxonGroupId == _taksongruppeB),
            skip: 0, take: 20);

        resultat.Select(o => o.Id).Should().Equal(forventet);
        resultat.Should().NotBeEmpty("snittet må ha treff for at testen skal bety noe");
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Fasiten hentes med ett enkelt uttrykk mot samme data, uten grening. Det
    /// er den eneste måten å skille «grenene er raske» fra «grenene er riktige».
    /// </summary>
    private async Task<List<int>> ForventetAsync(
        System.Linq.Expressions.Expression<Func<Observation, bool>> predikat, int skip, int take)
        => await _context.Set<Observation>()
            .AsNoTracking()
            .Where(predikat)
            .OrderByDescending(o => o.DateTimeCollected)
            .ThenByDescending(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Select(o => o.Id)
            .ToListAsync();

    private async Task SeedAsync()
    {
        _lokasjon = await EnsureLocationAsync();

        var organisasjoner = await EnsureOrganizationsAsync();
        _instA = organisasjoner[0];
        _instB = organisasjoner[1];
        _instMange = [.. organisasjoner];

        var taksongrupper = await _context.Set<Observation>()
            .Where(o => o.InstitutionOrgId == _instA)
            .Select(o => o.TaxonGroupId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync();

        if (taksongrupper.Count >= 2)
        {
            _taksongruppeA = taksongrupper[0];
            _taksongruppeB = taksongrupper[1];
            return;
        }

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();

        // To taksongrupper som ikke finnes fra før, så filteret ikke plukker opp
        // rader seedet av andre tester i samme base.
        var hoyeste = await _context.Set<Observation>().MaxAsync(o => (int?)o.TaxonGroupId) ?? 0;
        _taksongruppeA = hoyeste + 9001;
        _taksongruppeB = hoyeste + 9002;

        var nye = new List<Observation>();

        // Institusjon A: de nyeste radene, alle sammenhengende i datorekkefølgen.
        // Institusjon B: eldre, uten overlapp. Se kommentaren på feltene.
        for (var i = 0; i < AntallPerInstitusjon; i++)
        {
            nye.Add(NyObservasjon(taxon, taxonName, kategori, funntype,
                _instA, new DateTime(2021, 1, 1).AddDays(i),
                i % 2 == 0 ? _taksongruppeA : _taksongruppeB));

            nye.Add(NyObservasjon(taxon, taxonName, kategori, funntype,
                _instB, new DateTime(2019, 1, 1).AddDays(i),
                i % 2 == 0 ? _taksongruppeA : _taksongruppeB));
        }

        // De øvrige institusjonene får én rad hver, nok til at fallback-testen
        // har noe å hente uten å drukne de to første.
        for (var i = 2; i < organisasjoner.Count; i++)
        {
            nye.Add(NyObservasjon(taxon, taxonName, kategori, funntype,
                organisasjoner[i], new DateTime(2018, 6, 1).AddDays(i), _taksongruppeA));
        }

        _context.Set<Observation>().AddRange(nye);
        await _context.SaveChangesAsync();
    }

    private async Task<List<int>> EnsureOrganizationsAsync()
    {
        const string navnPrefiks = "Grentest institusjon ";

        var eksisterende = await _context.Set<Organization>()
            .Where(o => o.Name.StartsWith(navnPrefiks))
            .OrderBy(o => o.Name)
            .Select(o => o.Id)
            .ToListAsync();

        if (eksisterende.Count >= 9) return eksisterende;

        // OrganizationType har IKKE identity-nøkkel: en ny rad får Id 0, og den
        // er allerede tatt av en annen testklasse når hele suiten kjører. Typen
        // betyr ingenting for grenlogikken, så vi låner en som finnes.
        var typeId = await _context.Set<OrganizationType>()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync();

        // Tosifret nummer: sortert på navn skal 10 ikke komme før 2.
        var nye = Enumerable.Range(0, 9)
            .Select(i => new Organization
            {
                Name = $"{navnPrefiks}{i:D2}",
                OrganizationTypeId = typeId,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
            })
            .ToList();

        _context.Set<Organization>().AddRange(nye);
        await _context.SaveChangesAsync();

        return nye.Select(o => o.Id).ToList();
    }

    private async Task<Location> EnsureLocationAsync()
    {
        const string lookupId = "branch-loc-1";

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

    private Observation NyObservasjon(
        Taxon taxon, TaxonName taxonName, Entities.Category kategori, Entities.BasisOfRecord funntype,
        int institusjonId, DateTime dato, int taksongruppeId) => new()
    {
        DateLastModified = DateTime.UtcNow,
        DateTimeRecordImported = DateTime.UtcNow,
        DateTimeRecordProcessed = DateTime.UtcNow,
        DateTimeCollected = dato,
        NodeId = 1,
        BasisOfRecordId = funntype.Id,
        TaxonId = taxon.Id,
        MatchedScientificNameId = taxonName.Id,
        TaxonGroupId = taksongruppeId,
        CategoryId = kategori.Id,
        InstitutionOrgId = institusjonId,
        Latitude = _lokasjon.Latitude ?? 0,
        Longitude = _lokasjon.Longitude ?? 0,
        CoordinatePrecisionInMeters = 25,
        East = _lokasjon.East,
        North = _lokasjon.North,
        LocationId = _lokasjon.Id,
        HashCode = 0,
        ProcessEngineId = 1,
        HasErrors = false,
        HasAnnotations = false,
    };
}
