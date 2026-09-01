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
    public void ApplyFilters_WithCollectionOrgId_ReturnsOnlyMatchingCollection()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context,
            CreateObservation(1, collectionOrgId: 100),
            CreateObservation(2, collectionOrgId: 200),
            CreateObservation(3, collectionOrgId: null));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { CollectionOrgId = 100 });

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
    public void ApplyFilters_WithDatasetOrgId_ReturnsObservationsInThatDataset()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        context.Set<ObservationDataset>().AddRange(
            new ObservationDataset { ObservationId = 1, DatasetOrgId = 500 },
            new ObservationDataset { ObservationId = 2, DatasetOrgId = 600 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { DatasetOrgId = 500 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    /// <summary>
    /// Datasett er IKKE 1:1 — 745 066 observasjoner tilhører mellom to og fem.
    /// Derfor er koblingen en egen tabell og ikke en kolonne, og derfor må
    /// filteret være et semi-join: et join ville gitt observasjonen én gang per
    /// datasett, altså duplikater i eksporten og for høy forhåndstelling.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithDatasetOrgId_DoesNotDuplicateMultiDatasetObservations()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1));
        context.Set<ObservationDataset>().AddRange(
            new ObservationDataset { ObservationId = 1, DatasetOrgId = 500 },
            new ObservationDataset { ObservationId = 1, DatasetOrgId = 501 },
            new ObservationDataset { ObservationId = 1, DatasetOrgId = 502 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { DatasetOrgId = 500 });

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public void ApplyFilters_WithDatasetOrgId_ExcludesObservationsWithNoDatasetRows()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context, CreateObservation(1), CreateObservation(2));
        context.Set<ObservationDataset>().Add(new ObservationDataset { ObservationId = 1, DatasetOrgId = 500 });
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto { DatasetOrgId = 500 });

        result.Select(o => o.Id).Should().BeEquivalentTo([1]);
    }

    /// <summary>
    /// Flere identifikatorfiltre skal snevre inn, ikke utvide. De legges på som
    /// separate Where-kall og må derfor virke som OG.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithCollectionAndObservationIds_CombinesWithAnd()
    {
        using var context = CreateInMemoryContext();
        SeedObservations(context,
            CreateObservation(1, collectionOrgId: 100),
            CreateObservation(2, collectionOrgId: 200));
        context.SaveChanges();

        var result = Apply(context, new ObservationSearchFilterDto
        {
            CollectionOrgId = 100,
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

    private static Observation CreateObservation(int id, int? collectionOrgId = null) =>
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
            CollectionOrgId = collectionOrgId,
            HashCode = id,
            ProcessEngineId = 1,
            HasAnnotations = false,
            HasErrors = false
        };
}
