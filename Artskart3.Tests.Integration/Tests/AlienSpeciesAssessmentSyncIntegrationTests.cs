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
    private readonly List<int> _createdSyncRunIds = [];
    private int _scientificNameId;
    private int _snapshotScientificNameId;
    private int _duplicateTaxonId;
    private int _unassessedObservationId;
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
        if (_createdSyncRunIds.Count > 0)
        {
            await _context.Set<TaxonAssessmentSyncRun>()
                .Where(run => _createdSyncRunIds.Contains(run.Id))
                .ExecuteDeleteAsync();
        }
        if (_scientificNameId > 0)
        {
            await _context.Set<TaxonAssessment>()
                .Where(assessment =>
                    assessment.ScientificNameId == _scientificNameId ||
                    assessment.ScientificNameId == _snapshotScientificNameId)
                .ExecuteDeleteAsync();
        }
        await _context.Set<ObservationEntityIndex>()
            .Where(index =>
                index.ObservationId == _observationId ||
                index.ObservationId == _unassessedObservationId)
            .ExecuteDeleteAsync();
        await _context.Observations
            .Where(observation => observation.Id == _observationId)
            .ExecuteDeleteAsync();
        await _context.Observations
            .Where(observation => observation.Id == _unassessedObservationId)
            .ExecuteDeleteAsync();

        if (_duplicateTaxonId > 0)
        {
            await _context.TaxonAssessments
                .Where(assessment => assessment.TaxonId == _duplicateTaxonId)
                .ExecuteDeleteAsync();
            await _context.Taxons
                .Where(taxon => taxon.Id == _duplicateTaxonId)
                .ExecuteDeleteAsync();
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
        var sourceTaxonName = await _context.TaxonNames
            .Include(name => name.Taxon)
            .Where(name => !name.IsDeleted && name.TaxonId.HasValue && name.Taxon != null && !name.Taxon.IsDeleted)
            .FirstAsync();
        var snapshotOnlyTaxonName = await _context.TaxonNames
            .Where(name =>
                !name.IsDeleted &&
                name.TaxonId.HasValue &&
                name.Taxon != null &&
                !name.Taxon.IsDeleted &&
                name.Id != sourceTaxonName.Id &&
                name.TaxonId != sourceTaxonName.TaxonId)
            .FirstAsync();
        var taxon = sourceTaxonName.Taxon!;
        var basisOfRecord = await _context.BasisOfRecords.FirstAsync();
        _scientificNameId = sourceTaxonName.Id;
        _snapshotScientificNameId = snapshotOnlyTaxonName.Id;
        _duplicateTaxonId = 2_147_400_000;
        _context.Taxons.Add(new Taxon
        {
            Id = _duplicateTaxonId,
            ExternalTaxonId = 0,
            TaxonRankId = taxon.TaxonRankId,
            ParentTaxonId = 0,
            ValidScientificNameId = sourceTaxonName.Id,
            ValidScientificName = taxon.ValidScientificName,
            TaxonGroupId = taxon.TaxonGroupId,
            ExistsInCountry = true,
            ScientificNameIdHiarchy = taxon.ValidScientificNameId.ToString(),
            TaxonIdHiarchy = _duplicateTaxonId.ToString()
        });
        _context.TaxonAssessments.Add(new TaxonAssessment
        {
            ScientificNameId = 990_000_004,
            TaxonId = _duplicateTaxonId,
            CategoryTypeId = categoryType.Id,
            CategoryId = alienCategory.Id,
            Area = "N",
            ExternalAssessmentId = "101",
            Source = TaxonAssessmentSource.Current,
            AssessmentUrl = "https://lister.artsdatabanken.no/arter/fremmedartslista/101",
            LastSeenAt = DateTime.UtcNow.AddDays(-7),
            IsEffective = true
        });
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
            MatchedScientificNameId = sourceTaxonName.Id,
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
        var unassessedObservation = new Observation
        {
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            TaxonId = _duplicateTaxonId,
            MatchedScientificNameId = sourceTaxonName.Id,
            TaxonGroupId = taxon.TaxonGroupId,
            CategoryId = redCategory.Id,
            BasisOfRecordId = basisOfRecord.Id,
            Latitude = 60,
            Longitude = 10,
            East = 100,
            North = 100,
            HashCode = 2,
            ProcessEngineId = 1
        };
        _context.Observations.Add(unassessedObservation);
        await _context.SaveChangesAsync();
        _unassessedObservationId = unassessedObservation.Id;
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = unassessedObservation.Id,
            EntityTypeId = 1,
            EntityId = 1,
            TaxonGroupId = taxon.TaxonGroupId,
            CategoryId = redCategory.Id,
            BasisOfRecordId = basisOfRecord.Id,
            RegistrationStatusId = 1
        });
        await _context.SaveChangesAsync();

        var client = new StubAlienSpeciesAssessmentClient(
            [new CurrentAlienSpeciesAssessment(
                101,
                "N",
                "LO",
                new AlienSpeciesScientificNameInformation(990_000_003),
                [new AlienSpeciesPreviousAssessment(2023, "2023")])],
            [
                new AlienSpeciesAssessment2023(
                    2023,
                    "N",
                    new AlienSpeciesScientificName(sourceTaxonName.Id),
                    "HI"),
                new AlienSpeciesAssessment2023(
                    2024,
                    "N",
                    new AlienSpeciesScientificName(snapshotOnlyTaxonName.Id),
                    "NR")
            ]);
        var service = new AlienSpeciesAssessmentSyncService(
            _context,
            client,
            Options.Create(new AlienSpeciesAssessmentOptions
            {
                IndexProjectionBatchSize = 1,
                IndexZeroProjectionBatchSize = 1,
                IndexProjectionCommandTimeoutSeconds = 60,
                ResetAssessmentsBeforeSync = true
            }),
            NullLogger<AlienSpeciesAssessmentSyncService>.Instance);

        var run = await service.SynchronizeAsync();
        _createdSyncRunIds.Add(run!.Id);

        var observation = await _context.Observations.AsNoTracking()
            .SingleAsync(item => item.Id == _observationId);
        var assessments = await _context.TaxonAssessments.AsNoTracking()
            .Where(item =>
                !item.IsDeleted &&
                (item.ScientificNameId == sourceTaxonName.Id ||
                 item.ScientificNameId == snapshotOnlyTaxonName.Id))
            .ToListAsync();
        var index = await _context.Set<ObservationEntityIndex>().AsNoTracking()
            .SingleAsync(item => item.ObservationId == _observationId);

        observation.CategoryId.Should().Be(redCategory.Id);
        index.AlienSpeciesCategoryId.Should().Be(alienCategory.Id);
        var unassessedIndex = await _context.Set<ObservationEntityIndex>().AsNoTracking()
            .SingleAsync(item => item.ObservationId == _unassessedObservationId);
        unassessedIndex.AlienSpeciesCategoryId.Should().Be(0);
        assessments.Should().HaveCount(3);
        var currentAssessment = assessments.Single(item => item.Source == TaxonAssessmentSource.Current);
        currentAssessment.TaxonId.Should().Be(taxon.Id);
        currentAssessment
            .ScientificNameId.Should().Be(_scientificNameId);
        currentAssessment.AssessmentUrl.Should().Be(
            "https://lister.artsdatabanken.no/arter/fremmedartslista/101");
        var updatedCategory = await _context.Categories.AsNoTracking()
            .SingleAsync(category => category.Id == alienCategory.Id);
        updatedCategory.ObservationCount.Should().BeGreaterThanOrEqualTo(1);
        assessments.Where(item => item.IsEffective).Should().HaveCount(2);
        var snapshotAssessment = assessments.Single(
            item =>
                item.Source == TaxonAssessmentSource.Snapshot2023 &&
                item.ScientificNameId == snapshotOnlyTaxonName.Id);
        snapshotAssessment.AssessmentUrl.Should().Be(
            "https://lister.artsdatabanken.no/fremmedartslista/2023/2024");

        var weeklyClient = new StubAlienSpeciesAssessmentClient(
            [new CurrentAlienSpeciesAssessment(
                101,
                "N",
                "LO",
                new AlienSpeciesScientificNameInformation(990_000_005),
                [new AlienSpeciesPreviousAssessment(2023, "2023")])],
            []);
        var weeklyService = new AlienSpeciesAssessmentSyncService(
            _context,
            weeklyClient,
            Options.Create(new AlienSpeciesAssessmentOptions()),
            NullLogger<AlienSpeciesAssessmentSyncService>.Instance);

        var weeklyRun = await weeklyService.SynchronizeAsync();
        _createdSyncRunIds.Add(weeklyRun!.Id);

        var continuityAssessment = await _context.TaxonAssessments.AsNoTracking()
            .Where(assessment =>
                !assessment.IsDeleted &&
                assessment.Source == TaxonAssessmentSource.Current &&
                assessment.ExternalAssessmentId == "101")
            .SingleAsync();
        continuityAssessment.TaxonId.Should().Be(taxon.Id);
        continuityAssessment.ScientificNameId.Should().Be(sourceTaxonName.Id);
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
