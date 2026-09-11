using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using TagEnum = Artskart3.Core.Domain.Enums.Tag;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Samling, prosjekt/datasett og katalognummer i eksportstien.
///
/// Filtrene manglet her mens de fantes i søket. Symptomet var ikke en for stor
/// eksport, men en avvist: forhåndstellingen i ExportService talte alle rader og
/// traff radgrensen, så en bruker som hadde filtrert ned til noen få treff fikk
/// «Antall rader overstiger grensen» i stedet for en fil.
/// </summary>
public class ObservationQueryBuilderTests
{
    [Fact]
    public void ApplyFilters_WithNoFilters_ReturnsEverything()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto());

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyFilters_WithDatasetOrgId_ReturnsOnlyMatchingDataset()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context,
            CreateObservation(1, datasetOrgId: 100),
            CreateObservation(2, datasetOrgId: 200),
            CreateObservation(3, datasetOrgId: null));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { DatasetOrgId = 100 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public void ApplyFilters_WithObservationIds_ReturnsOnlyThoseIds()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2), CreateObservation(3));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { ObservationIds = [1, 3] });

        result.Select(o => o.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void ApplyFilters_WithProjectOrgId_ReturnsObservationsInThatProject()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        context.Set<ObservationProject>().AddRange(
            new ObservationProject { ObservationId = 1, ProjectOrgId = 500 },
            new ObservationProject { ObservationId = 2, ProjectOrgId = 600 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { ProjectOrgId = 500 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    /// <summary>
    /// Datasett er IKKE 1:1 — 745 066 observasjoner tilhører mellom to og fem.
    /// Derfor er koblingen en egen tabell og ikke en kolonne, og derfor må
    /// filteret være et semi-join: et join ville gitt observasjonen én gang per
    /// datasett, altså duplikater i eksporten og for høy forhåndstelling.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithProjectOrgId_DoesNotDuplicateMultiProjectObservations()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1));
        context.Set<ObservationProject>().AddRange(
            new ObservationProject { ObservationId = 1, ProjectOrgId = 500 },
            new ObservationProject { ObservationId = 1, ProjectOrgId = 501 },
            new ObservationProject { ObservationId = 1, ProjectOrgId = 502 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { ProjectOrgId = 500 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public void ApplyFilters_WithProjectOrgId_ExcludesObservationsWithNoProjectRows()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        context.Set<ObservationProject>().Add(new ObservationProject { ObservationId = 1, ProjectOrgId = 500 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { ProjectOrgId = 500 });

        result.Select(o => o.Id).Should().BeEquivalentTo([1]);
    }

    /// <summary>
    /// Flere identifikatorfiltre skal snevre inn, ikke utvide. De legges på som
    /// separate Where-kall og må derfor virke som OG.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithDatasetAndObservationIds_CombinesWithAnd()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context,
            CreateObservation(1, datasetOrgId: 100),
            CreateObservation(2, datasetOrgId: 200));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto
        {
            DatasetOrgId = 100,
            ObservationIds = [2]
        });

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Taksonfilter (TaxonIds)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Utvidelsen skal gå via ObservationTaxonHierarchy, ikke via o.TaxonId.
    ///
    /// Observasjon 1 har TaxonId 1, men er registrert under art 500 i
    /// hierarkitabellen. Et naivt <c>TaxonIds.Contains(o.TaxonId)</c> ville ikke
    /// funnet den — og det er nettopp forskjellen mellom å treffe det brukeren så
    /// på skjermen og å treffe noe annet.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithTaxonIds_MatchesViaHierarchyNotObservationTaxonId()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        SeedHierarchy(context,
            Hierarchy(1, speciesTaxonId: 500),
            Hierarchy(2, speciesTaxonId: 501));
        context.SaveChanges();

        var result = Apply(context,
            new ObservationSearchFilterDto { TaxonIds = [500] },
            RanksOf((500, 22)));

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    /// <summary>
    /// Velger man en familie i taksontreet, forventes alle etterkommere. Rangen
    /// avgjør hvilken kolonne i hierarkitabellen som slås opp — her FamilyTaxonId.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithHigherRankTaxonId_IncludesAllDescendants()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2), CreateObservation(3));
        SeedHierarchy(context,
            Hierarchy(1, familyTaxonId: 700, speciesTaxonId: 500),
            Hierarchy(2, familyTaxonId: 700, speciesTaxonId: 501),
            Hierarchy(3, familyTaxonId: 701, speciesTaxonId: 502));
        context.SaveChanges();

        var result = Apply(context,
            new ObservationSearchFilterDto { TaxonIds = [700] },
            RanksOf((700, 15)));

        result.Select(o => o.Id).Should().BeEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Flere valgte taxoner er et ELLER seg imellom — brukeren har lagt til to
    /// arter i filteret og forventer begge, ikke skjæringen (som alltid er tom).
    /// </summary>
    [Fact]
    public void ApplyFilters_WithMultipleTaxonIds_UnionsThem()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2), CreateObservation(3));
        SeedHierarchy(context,
            Hierarchy(1, speciesTaxonId: 500),
            Hierarchy(2, speciesTaxonId: 501),
            Hierarchy(3, speciesTaxonId: 502));
        context.SaveChanges();

        var result = Apply(context,
            new ObservationSearchFilterDto { TaxonIds = [500, 502] },
            RanksOf((500, 22), (502, 22)));

        result.Select(o => o.Id).Should().BeEquivalentTo([1, 3]);
    }

    /// <summary>
    /// Ukjent taxonId gir ingen rang, og da skal filteret gi tomt — ikke slippe
    /// alt gjennom. Åpner det opp i stedet, blir en filtrert eksport plutselig
    /// en eksport av hele tabellen.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithUnknownTaxonRank_ReturnsNothing()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1));
        SeedHierarchy(context, Hierarchy(1, speciesTaxonId: 500));
        context.SaveChanges();

        var result = Apply(context,
            new ObservationSearchFilterDto { TaxonIds = [999] },
            RanksOf());

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Registreringsstatus
    // ---------------------------------------------------------------------

    /// <summary>
    /// Status 1 («funnet») er et negativt vilkår: alt som verken er Absent eller
    /// NotRecovered. Manglet filteret, fikk eksporten med nøyaktig de radene
    /// brukeren hadde filtrert bort.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithRegistrationStatusFound_ExcludesAbsentAndNotRecovered()
    {
        using var context = CreateInMemoryContext();
        SeedObservationsWithTags(context,
            (CreateObservation(1), []),
            (CreateObservation(2), [(int)TagEnum.Absent]),
            (CreateObservation(3), [(int)TagEnum.NotRecovered]));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { RegistrationStatusId = 1 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    /// <summary>
    /// Status 2 = «ikke registrert» (Absent), status 3 = «ikke gjenfunnet»
    /// (NotRecovered). Observasjon 2 og 3 er seedet med henholdsvis den ene og
    /// den andre taggen.
    /// </summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void ApplyFilters_WithRegistrationStatus_ReturnsOnlyThatTag(int statusId, int expectedObservationId)
    {
        using var context = CreateInMemoryContext();
        SeedObservationsWithTags(context,
            (CreateObservation(1), []),
            (CreateObservation(2), [(int)TagEnum.Absent]),
            (CreateObservation(3), [(int)TagEnum.NotRecovered]));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { RegistrationStatusId = statusId });

        result.Should().ContainSingle().Which.Id.Should().Be(expectedObservationId);
    }

    // ---------------------------------------------------------------------
    // Bildefilter
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void ApplyFilters_WithImages_SplitsOnMediaFiles(bool withImages, int expectedId)
    {
        using var context = CreateInMemoryContext();
        var withMedia = CreateObservation(1);
        var withoutMedia = CreateObservation(2);
        SeedObservations(context, withMedia, withoutMedia);
        context.Set<MediaFile>().Add(new MediaFile
        {
            Id = 1,
            ObservationId = 1,
            Origin = "test",
            MediaFileTypeId = 1
        });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { WithImages = withImages });

        result.Should().ContainSingle().Which.Id.Should().Be(expectedId);
    }

    // ---------------------------------------------------------------------
    // Paritet mot søket
    // ---------------------------------------------------------------------

    /// <summary>
    /// Vaktposten mot den feilklassen som stadig kommer tilbake: et filterfelt
    /// finnes i søket, men ignoreres i eksportstien.
    ///
    /// Konsekvensen er ikke bare feil innhold i filen. Forhåndstellingen i
    /// ExportService bruker samme spørring, så et ignorert felt gjør at en
    /// nedfiltrert søkning telles som hele tabellen, treffer radgrensen og gir
    /// brukeren «Eksport ikke mulig» i stedet for en fil.
    ///
    /// Testen går gjennom HVERT felt på IObservationFilter og setter det til en
    /// verdi som ikke matcher den seedede observasjonen. Blir observasjonen med
    /// likevel, er feltet ikke tatt hensyn til. Legges det til et nytt felt på
    /// grensesnittet, feiler testen på dekningssjekken under helt til noen har
    /// tatt stilling til det.
    /// </summary>
    [Fact]
    public void ApplyFilters_AppliesEveryFieldOnObservationFilter()
    {
        var nonMatching = NonMatchingFilterValues();

        var interfaceProperties = typeof(IObservationFilter)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        interfaceProperties.Should().BeSubsetOf(nonMatching.Keys,
            "hvert felt på IObservationFilter må ha en ikke-matchende verdi her, " +
            "ellers er det ingen som sjekker at eksporten faktisk bruker feltet");

        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1, datasetOrgId: 100));
        SeedHierarchy(context, Hierarchy(1, speciesTaxonId: 500));
        context.SaveChanges();

        // Uten filtre skal observasjonen være med — ellers sier testene under ingenting.
        Apply(context, new ObservationSearchFilterDto()).Should().ContainSingle();

        using var scope = new AssertionScope();
        foreach (var propertyName in interfaceProperties)
        {
            var filter = new ObservationSearchFilterDto();
            nonMatching[propertyName](filter);

            Apply(context, filter, RanksOf((500, 22)))
                .Should().BeEmpty($"filteret {propertyName} må brukes i eksportstien");
        }
    }

    /// <summary>
    /// Én verdi per filterfelt som IKKE skal matche observasjonen seedet over.
    /// </summary>
    private static Dictionary<string, Action<ObservationSearchFilterDto>> NonMatchingFilterValues() => new()
    {
        [nameof(IObservationFilter.TaxonGroupIds)] = f => f.TaxonGroupIds = [999],
        [nameof(IObservationFilter.TaxonIds)] = f => f.TaxonIds = [501],
        [nameof(IObservationFilter.CategoryIds)] = f => f.CategoryIds = [999],
        [nameof(IObservationFilter.OrganizationIds)] = f => f.OrganizationIds = [999],
        [nameof(IObservationFilter.MunicipalityIds)] = f => f.MunicipalityIds = ["9999"],
        [nameof(IObservationFilter.CountyIds)] = f => f.CountyIds = ["99"],
        [nameof(IObservationFilter.RestrictedAreaIds)] = f => f.RestrictedAreaIds = ["Naturbase VV9999"],
        [nameof(IObservationFilter.OceanAreaIds)] = f => f.OceanAreaIds = ["9999"],
        [nameof(IObservationFilter.BehaviorIds)] = f => f.BehaviorIds = [999],
        [nameof(IObservationFilter.BasisOfRecordIds)] = f => f.BasisOfRecordIds = [999],
        // Observasjonen har ingen tagger, så «ikke registrert» treffer ikke.
        [nameof(IObservationFilter.RegistrationStatusId)] = f => f.RegistrationStatusId = 2,
        [nameof(IObservationFilter.CoordinatePrecision)] = f => f.CoordinatePrecision = new CoordinatePrecisionDto { From = 99_999 },
        [nameof(IObservationFilter.Period)] = f => f.Period = new PeriodDto { From = 2999 },
        [nameof(IObservationFilter.DatasetOrgId)] = f => f.DatasetOrgId = 999,
        [nameof(IObservationFilter.ProjectOrgId)] = f => f.ProjectOrgId = 999,
        [nameof(IObservationFilter.ObservationIds)] = f => f.ObservationIds = [999],
        // Observasjonen har ingen mediefiler.
        [nameof(IObservationFilter.WithImages)] = f => f.WithImages = true,
    };

    private static List<Observation> Apply(
        ArtskartDbContext context,
        ObservationSearchFilterDto filter,
        ITaxonHierarchyService? taxonHierarchy = null) =>
        ObservationQueryBuilder
            .ApplyFilters(
                context,
                taxonHierarchy ?? new StubTaxonHierarchyService(),
                context.Set<Observation>().AsNoTracking(),
                filter)
            .ToList();

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static void SeedObservations(ArtskartDbContext context, params Observation[] observations) =>
        context.Set<Observation>().AddRange(observations);

    private static void SeedObservationsWithTags(
        ArtskartDbContext context,
        params (Observation Observation, int[] TagIds)[] rows)
    {
        // Tagger deles på tvers av observasjoner, så de må gjenbrukes og ikke
        // legges til én gang per observasjon.
        var tags = new Dictionary<int, Tag>();

        foreach (var (observation, tagIds) in rows)
        {
            foreach (var tagId in tagIds)
            {
                if (!tags.TryGetValue(tagId, out var tag))
                {
                    tag = new Tag { Id = tagId, Name = $"tag-{tagId}" };
                    tags[tagId] = tag;
                    context.Set<Tag>().Add(tag);
                }

                observation.Tags.Add(tag);
            }

            context.Set<Observation>().Add(observation);
        }
    }

    private static void SeedHierarchy(ArtskartDbContext context, params ObservationTaxonHierarchy[] rows) =>
        context.Set<ObservationTaxonHierarchy>().AddRange(rows);

    private static ObservationTaxonHierarchy Hierarchy(
        int observationId,
        int? familyTaxonId = null,
        int? speciesTaxonId = null) =>
        new()
        {
            ObservationId = observationId,
            FamilyTaxonId = familyTaxonId,
            SpeciesTaxonId = speciesTaxonId
        };

    /// <summary>
    /// Rangoppslag med kjente rangnivåer. Den delte StubTaxonHierarchyService
    /// svarer alltid 22, som ikke duger når testen nettopp handler om at rangen
    /// bestemmer hvilken kolonne i hierarkitabellen som slås opp.
    /// </summary>
    private static ITaxonHierarchyService RanksOf(params (int TaxonId, int RankId)[] ranks) =>
        new ConfigurableTaxonHierarchyService(ranks.ToDictionary(r => r.TaxonId, r => r.RankId));

    private sealed class ConfigurableTaxonHierarchyService(Dictionary<int, int> ranks) : ITaxonHierarchyService
    {
        public int? GetTaxonRankId(int taxonId) => ranks.TryGetValue(taxonId, out var rank) ? rank : null;

        public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId) => [];

        public List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds) =>
            taxonIds.Select(id => new TaxonAncestryDto { Id = id, ParentIds = [] }).ToList();

        public List<int> GetDescendantSpeciesIds(int taxonId) => [];

        public List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId) => [];
    }

    private static Observation CreateObservation(int id, int? datasetOrgId = null) =>
        new()
        {
            Id = id,
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            BasisOfRecordId = 1,
            TaxonId = 1,
            MatchedScientificNameId = 1,
            TaxonGroupId = 1,
            CategoryId = 1,
            Latitude = 59.91,
            Longitude = 10.75,
            CoordinatePrecisionInMeters = 25,
            East = 1000,
            North = 2000,
            LocationId = 1,
            InstitutionOrgId = 1,
            DatasetOrgId = datasetOrgId,
            HashCode = id,
            ProcessEngineId = 1,
            HasAnnotations = false,
            HasErrors = false
        };
}
