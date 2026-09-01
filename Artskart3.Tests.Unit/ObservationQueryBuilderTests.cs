using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

    private static List<Observation> Apply(ArtskartDbContext context, ObservationSearchFilterDto filter) =>
        ObservationQueryBuilder
            .ApplyFilters(context, context.Set<Observation>().AsNoTracking(), filter)
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
