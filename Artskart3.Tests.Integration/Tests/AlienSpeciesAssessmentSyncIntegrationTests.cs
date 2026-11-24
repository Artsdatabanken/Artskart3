using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services.AlienSpecies;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class AlienSpeciesAssessmentSyncIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Dictionary<int, int?> _originalCategoryCounts = [];
    private int? _createdSyncRunId;
    private int _externalTaxonId;
    private int _testTaxonId;
    private int _originalExternalTaxonId;
    private int _originalValidScientificNameId;
    private int _observationId;

    public AlienSpeciesAssessmentSyncIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .Options;
        _context = new ArtskartDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_createdSyncRunId is { } runId)
        {
            await _context.Set<TaxonAssessmentSyncRun>()
                .Where(run => run.Id == runId)
                .ExecuteDeleteAsync();
        }
        if (_externalTaxonId > 0)
        {
            await _context.Set<TaxonAssessment>()
                .Where(assessment => assessment.ExternalTaxonId == _externalTaxonId)
                .ExecuteDeleteAsync();
        }
        await _context.Set<ObservationEntityIndex>()
            .Where(index => index.ObservationId == _observationId)
            .ExecuteDeleteAsync();
        await _context.Observations
            .Where(observation => observation.Id == _observationId)
            .ExecuteDeleteAsync();

        if (_testTaxonId > 0)
        {
            await _context.Taxons
                .Where(taxon => taxon.Id == _testTaxonId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(taxon => taxon.ExternalTaxonId, _originalExternalTaxonId)
                    .SetProperty(taxon => taxon.ValidScientificNameId, _originalValidScientificNameId));
        }

        foreach (var (categoryId, count) in _originalCategoryCounts)
        {
            await _context.Categories
                .Where(category => category.Id == categoryId)
                .ExecuteUpdateAsync(update => update.SetProperty(category => category.ObservationCount, count));
        }

        await _context.DisposeAsync();
    }

    [Fact]
    public async Task SynchronizeAsync_PublishesCurrentPrecedenceAndObservationProjection()
    {
        var categoryType = await _context.CategoryTypes.SingleAsync(type => type.Name == "Fremmedart");
        var alienCategory = await _context.Categories
            .SingleAsync(category => category.CategoryTypeId == categoryType.Id && category.Code == "LO");
        var redCategory = await _context.Categories
            .Where(category => category.CategoryType.Name == "Rødliste")
            .FirstAsync();
        var taxon = await _context.Taxons.FirstAsync(item => !item.IsDeleted);
        _testTaxonId = taxon.Id;
        _originalExternalTaxonId = taxon.ExternalTaxonId;
        _originalValidScientificNameId = taxon.ValidScientificNameId;
        taxon.ExternalTaxonId = 990_000_001;
        taxon.ValidScientificNameId = 990_000_002;
        var matchedName = await _context.TaxonNames.FirstAsync();
        var basisOfRecord = await _context.BasisOfRecords.FirstAsync();
        _externalTaxonId = taxon.ExternalTaxonId;
        _originalCategoryCounts = await _context.Categories
            .Where(category => category.CategoryTypeId == categoryType.Id)
            .ToDictionaryAsync(category => category.Id, category => category.ObservationCount);

        var observationEntity = new Observation
        {
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            TaxonId = taxon.Id,
            MatchedScientificNameId = matchedName.Id,
            TaxonGroupId = taxon.TaxonGroupId,
            CategoryId = redCategory.Id,
            BasisOfRecordId = basisOfRecord.Id,
            Latitude = 60,
            Longitude = 10,
            East = 100,
            North = 100,
            HashCode = 1,
            ProcessEngineId = 1
        };
        _context.Observations.Add(observationEntity);
        await _context.SaveChangesAsync();
        _observationId = observationEntity.Id;

        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = observationEntity.Id,
            EntityTypeId = 1,
            EntityId = 1,
            TaxonGroupId = taxon.TaxonGroupId,
            CategoryId = redCategory.Id,
            BasisOfRecordId = basisOfRecord.Id,
            RegistrationStatusId = 1
        });
        await _context.SaveChangesAsync();

        var client = new StubAlienSpeciesAssessmentClient(
            [new CurrentAlienSpeciesAssessment(101, "N", taxon.ExternalTaxonId, "LO")],
            [
                new AlienSpeciesAssessment2023(
                    2023,
                    "N",
                    new AlienSpeciesScientificName(taxon.ValidScientificNameId),
                    "HI")
            ]);
        var service = new AlienSpeciesAssessmentSyncService(
            _context,
            client,
            Options.Create(new AlienSpeciesAssessmentOptions()),
            NullLogger<AlienSpeciesAssessmentSyncService>.Instance);

        var run = await service.SynchronizeAsync();
        _createdSyncRunId = run!.Id;

        var observation = await _context.Observations.AsNoTracking()
            .SingleAsync(item => item.Id == _observationId);
        var index = await _context.Set<ObservationEntityIndex>().AsNoTracking()
            .SingleAsync(item => item.ObservationId == _observationId);
        var assessments = await _context.TaxonAssessments.AsNoTracking()
            .Where(item => item.ExternalTaxonId == taxon.ExternalTaxonId)
            .ToListAsync();

        observation.CategoryId.Should().Be(redCategory.Id);
        observation.AlienSpeciesCategoryId.Should().Be(alienCategory.Id);
        index.CategoryId.Should().Be(redCategory.Id);
        index.AlienSpeciesCategoryId.Should().Be(alienCategory.Id);
        assessments.Single(item => item.IsEffective).Source.Should().Be(TaxonAssessmentSource.Current);
        assessments.Single(item => item.Source == TaxonAssessmentSource.Snapshot2023)
            .AssessmentUrl.Should().BeNull();
    }

    private sealed class StubAlienSpeciesAssessmentClient(
        IReadOnlyList<CurrentAlienSpeciesAssessment> current,
        IReadOnlyList<AlienSpeciesAssessment2023> snapshot) : IAlienSpeciesAssessmentClient
    {
        public Task<TaxonAssessmentFetchResult<CurrentAlienSpeciesAssessment>> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaxonAssessmentFetchResult<CurrentAlienSpeciesAssessment>(current, 1));

        public Task<TaxonAssessmentFetchResult<AlienSpeciesAssessment2023>> GetSnapshot2023Async(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaxonAssessmentFetchResult<AlienSpeciesAssessment2023>(snapshot, 1));
    }
}
