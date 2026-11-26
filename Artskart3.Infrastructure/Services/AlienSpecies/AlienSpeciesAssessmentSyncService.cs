using System.Data;
using System.Text.Json;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AssessmentCategory = Artskart3.Core.Domain.Entities.Category;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public sealed class AlienSpeciesAssessmentSyncService(
    IArtsKartDbContext context,
    IAlienSpeciesAssessmentClient client,
    IOptions<AlienSpeciesAssessmentOptions> options,
    ILogger<AlienSpeciesAssessmentSyncService> logger)
{
    private readonly AlienSpeciesAssessmentOptions _options = options.Value;

    public async Task<TaxonAssessmentSyncRun?> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Synkronisering av fremmedartsvurderinger er deaktivert.");
            return null;
        }

        var categoryType = await context.Set<CategoryType>().AsNoTracking()
            .SingleOrDefaultAsync(
                type => !type.IsDeleted && type.Name == _options.CategoryTypeName,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Category type '{_options.CategoryTypeName}' does not exist.");

        var run = new TaxonAssessmentSyncRun
        {
            CategoryTypeId = categoryType.Id,
            Status = TaxonAssessmentSyncStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        context.Set<TaxonAssessmentSyncRun>().Add(run);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var currentFetch = await client.GetCurrentAsync(cancellationToken);
            if (currentFetch.Items.Count < _options.MinimumCurrentAssessmentCount)
            {
                throw new InvalidDataException(
                    $"Lister returned {currentFetch.Items.Count} current assessments; expected at least {_options.MinimumCurrentAssessmentCount}.");
            }

            var current = AlienSpeciesAssessmentNormalizer.NormalizeCurrent(currentFetch.Items);
            var refreshSnapshot = _options.ResetAssessmentsBeforeSync ||
                _options.ForceSnapshot2023Refresh ||
                !await context.Set<TaxonAssessmentSyncRun>()
                    .AsNoTracking()
                    .AnyAsync(
                        previous =>
                            previous.CategoryTypeId == categoryType.Id &&
                            previous.Status == TaxonAssessmentSyncStatus.Succeeded &&
                            previous.SnapshotAssessmentCount > 0,
                        cancellationToken);

            IReadOnlyList<NormalizedTaxonAssessment> snapshot = [];
            if (refreshSnapshot)
            {
                var snapshotFetch = await client.GetSnapshot2023Async(cancellationToken);
                if (snapshotFetch.Items.Count == 0)
                    throw new InvalidDataException("Lister returned an empty 2023 assessment snapshot.");
                snapshot = AlienSpeciesAssessmentNormalizer.NormalizeSnapshot2023(snapshotFetch.Items);
            }

            var allIncoming = current.Concat(snapshot).ToArray();
            var (resolvedTaxa, categoryIds) = await ResolveReferencesAsync(
                allIncoming, categoryType.Id, cancellationToken);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var allExisting = await context.Set<TaxonAssessment>()
                .Where(a => a.CategoryTypeId == categoryType.Id)
                .ToListAsync(cancellationToken);
            var existing = allExisting.Where(a => !a.IsDeleted).ToList();
            var effectiveBefore = GetEffectiveProjection(existing);

            if (_options.ResetAssessmentsBeforeSync)
            {
                context.Set<TaxonAssessment>().RemoveRange(allExisting);
                existing.Clear();
                logger.LogWarning(
                    "Nullstiller {Count} lagrede fremmedartsvurderinger før full synkronisering.",
                    allExisting.Count);
            }

            foreach (var assessment in existing.Where(a => a.Source == TaxonAssessmentSource.Current))
                assessment.IsRetained = true;

            if (refreshSnapshot)
            {
                foreach (var assessment in existing.Where(a => a.Source == TaxonAssessmentSource.Snapshot2023))
                    assessment.IsRetained = true;
            }

            var snapshotResult = refreshSnapshot
                ? ApplySourceRows(
                    snapshot,
                    TaxonAssessmentSource.Snapshot2023,
                    categoryType.Id,
                    resolvedTaxa,
                    categoryIds,
                    existing)
                : (Applied: 0, Skipped: 0);
            var currentResult = ApplySourceRows(
                current,
                TaxonAssessmentSource.Current,
                categoryType.Id,
                resolvedTaxa,
                categoryIds,
                existing);
            var appliedCount = snapshotResult.Applied + currentResult.Applied;
            var skippedCount = snapshotResult.Skipped + currentResult.Skipped;

            AlienSpeciesAssessmentMerger.SelectEffective(existing);
            var effectiveAfter = GetEffectiveProjection(existing);
            var changedTaxonIds = GetChangedTaxonIds(effectiveBefore, effectiveAfter);
            var effectiveTaxonIds = effectiveAfter.Keys
                .Select(key => key.TaxonId)
                .Distinct()
                .ToArray();
            var retainedCount = existing.Count(a => a.IsEffective && a.IsRetained);

            run.CurrentPageCount = currentFetch.PageCount;
            run.CurrentAssessmentCount = current.Count;
            run.SnapshotAssessmentCount = snapshot.Count;
            run.AppliedAssessmentCount = appliedCount;
            run.RetainedAssessmentCount = retainedCount;
            run.SkippedAssessmentCount = skippedCount;
            run.Status = TaxonAssessmentSyncStatus.Succeeded;
            run.CompletedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await ProjectObservationIndexCategoriesAsync(
                categoryType.Id,
                changedTaxonIds,
                effectiveTaxonIds,
                cancellationToken);
            await UpdateAlienCategoryObservationCountsAsync(
                categoryType.Id,
                cancellationToken);

            logger.LogInformation(
                "Fremmedartsvurderinger synkronisert. Gjeldende: {Current}, 2023: {Snapshot}, anvendt: {Applied}, beholdt: {Retained}, hoppet over: {Skipped}.",
                run.CurrentAssessmentCount, run.SnapshotAssessmentCount, run.AppliedAssessmentCount,
                run.RetainedAssessmentCount, run.SkippedAssessmentCount);
            return run;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (context is DbContext dbContext)
                dbContext.ChangeTracker.Clear();

            var failedRun = await context.Set<TaxonAssessmentSyncRun>()
                .SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            failedRun.Status = TaxonAssessmentSyncStatus.Failed;
            failedRun.CompletedAt = DateTime.UtcNow;
            failedRun.ErrorMessage = Truncate(ex.Message, 2000);
            await context.SaveChangesAsync(CancellationToken.None);
            logger.LogError(ex, "Synkronisering av fremmedartsvurderinger feilet i kjøring {RunId}.", failedRun.Id);
            throw;
        }
    }

    private async Task<(
        Dictionary<(TaxonAssessmentSource Source, int Identifier), ResolvedAssessmentTaxon> Taxa,
        Dictionary<string, int> CategoryIds)>
        ResolveReferencesAsync(
            IReadOnlyCollection<NormalizedTaxonAssessment> incoming,
            int categoryTypeId,
            CancellationToken cancellationToken)
    {
        var scientificNameIds = incoming
            .Select(a => a.SourceTaxonIdentifier)
            .Distinct()
            .ToArray();
        var taxonNames = await context.Set<TaxonName>().AsNoTracking()
            .Where(name =>
                !name.IsDeleted &&
                scientificNameIds.Contains(name.Id) &&
                name.TaxonId.HasValue &&
                name.Taxon != null &&
                !name.Taxon.IsDeleted)
            .Select(name => new
            {
                ScientificNameId = name.Id,
                TaxonId = name.TaxonId!.Value
            })
            .ToListAsync(cancellationToken);

        var resolved = new Dictionary<(TaxonAssessmentSource, int), ResolvedAssessmentTaxon>();
        foreach (var taxonName in taxonNames)
        {
            var match = new ResolvedAssessmentTaxon(
                taxonName.TaxonId,
                taxonName.ScientificNameId);
            resolved[(TaxonAssessmentSource.Current, taxonName.ScientificNameId)] = match;
            resolved[(TaxonAssessmentSource.Snapshot2023, taxonName.ScientificNameId)] = match;
        }

        var snapshotByAssessmentId = incoming
            .Where(assessment =>
                assessment.Source == TaxonAssessmentSource.Snapshot2023)
            .ToDictionary(
                assessment => assessment.ExternalAssessmentId,
                assessment => assessment,
                StringComparer.Ordinal);
        var previousSnapshotIds = incoming
            .Where(assessment =>
                assessment.Source == TaxonAssessmentSource.Current &&
                assessment.Previous2023AssessmentId is not null)
            .Select(assessment => assessment.Previous2023AssessmentId!)
            .Distinct()
            .ToArray();
        var persistedSnapshotAssessment = await context.Set<TaxonAssessment>()
            .AsNoTracking()
            .Where(assessment =>
                !assessment.IsDeleted &&
                assessment.Source == TaxonAssessmentSource.Snapshot2023 &&
                previousSnapshotIds.Contains(assessment.ExternalAssessmentId))
            .Select(assessment => new
            {
                assessment.ExternalAssessmentId,
                assessment.TaxonId,
                assessment.ScientificNameId
            })
            .ToDictionaryAsync(
                assessment => assessment.ExternalAssessmentId,
                assessment => new ResolvedAssessmentTaxon(
                    assessment.TaxonId,
                    assessment.ScientificNameId),
                StringComparer.Ordinal,
                cancellationToken);
        foreach (var current in incoming.Where(assessment =>
                     assessment.Source == TaxonAssessmentSource.Current &&
                     !resolved.ContainsKey((TaxonAssessmentSource.Current, assessment.SourceTaxonIdentifier)) &&
                     assessment.Previous2023AssessmentId is not null))
        {
            if (snapshotByAssessmentId.TryGetValue(current.Previous2023AssessmentId!, out var snapshot) &&
                resolved.TryGetValue(
                    (TaxonAssessmentSource.Snapshot2023, snapshot.SourceTaxonIdentifier),
                    out var previousTaxon))
            {
                resolved[(TaxonAssessmentSource.Current, current.SourceTaxonIdentifier)] = previousTaxon;
            }
            else if (persistedSnapshotAssessment.TryGetValue(
                         current.Previous2023AssessmentId!,
                         out var persistedPreviousTaxon))
            {
                resolved[(TaxonAssessmentSource.Current, current.SourceTaxonIdentifier)] =
                    persistedPreviousTaxon;
            }
        }

        var categoryCodes = incoming.Select(a => a.CategoryCode).Distinct().ToArray();
        var categoryIds = await context.Set<AssessmentCategory>().AsNoTracking()
            .Where(c => !c.IsDeleted && c.CategoryTypeId == categoryTypeId)
            .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

        var unknownCategories = categoryCodes.Except(categoryIds.Keys, StringComparer.Ordinal).ToArray();
        if (unknownCategories.Length > 0)
        {
            throw new InvalidDataException(
                $"Lister returned category codes without a local category: {string.Join(", ", unknownCategories)}.");
        }

        var skippedTaxa = incoming
            .Where(a => !resolved.ContainsKey((a.Source, a.SourceTaxonIdentifier)))
            .Select(a => (a.Source, a.SourceTaxonIdentifier))
            .Distinct()
            .Count();
        if (skippedTaxa > 0)
        {
            logger.LogWarning(
                "Hopper over {Count} vurderingstaksa som ikke finnes i Artskart.",
                skippedTaxa);
        }

        return (resolved, categoryIds);
    }

    private (int Applied, int Skipped) ApplySourceRows(
        IReadOnlyList<NormalizedTaxonAssessment> incoming,
        TaxonAssessmentSource source,
        int categoryTypeId,
        Dictionary<(TaxonAssessmentSource Source, int Identifier), ResolvedAssessmentTaxon> resolvedTaxa,
        Dictionary<string, int> categoryIds,
        List<TaxonAssessment> existing)
    {
        var byKey = existing
            .Where(assessment => !assessment.IsDeleted)
            .GroupBy(assessment => (
                assessment.TaxonId,
                assessment.Area,
                assessment.Source))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(assessment => assessment.UpdatedAt)
                    .First());
        var byAssessmentId = existing
            .Where(a => !a.IsDeleted && a.Source == source)
            .ToDictionary(a => a.ExternalAssessmentId, StringComparer.Ordinal);
        var skipped = 0;
        var applied = 0;

        foreach (var incomingRow in incoming)
        {
            if (!resolvedTaxa.TryGetValue(
                    (incomingRow.Source, incomingRow.SourceTaxonIdentifier),
                    out var resolvedTaxon))
            {
                skipped++;
                continue;
            }

            var sourceKey = (
                resolvedTaxon.TaxonId,
                incomingRow.Area,
                source);
            var hasExistingKey = byKey.TryGetValue(
                sourceKey,
                out var row);
            var hasExistingAssessmentId = byAssessmentId.TryGetValue(
                incomingRow.ExternalAssessmentId,
                out var assessmentRow);
            if (!hasExistingKey && hasExistingAssessmentId)
            {
                byKey.Remove((assessmentRow!.TaxonId, assessmentRow.Area, source));
                assessmentRow.Area = incomingRow.Area;
                assessmentRow.TaxonId = resolvedTaxon.TaxonId;
                assessmentRow.ScientificNameId = resolvedTaxon.ScientificNameId;
                row = assessmentRow;
                byKey[(row.TaxonId, row.Area, source)] = row;
                hasExistingKey = true;
            }
            else if (hasExistingKey &&
                     hasExistingAssessmentId &&
                     !ReferenceEquals(row, assessmentRow))
            {
                assessmentRow!.IsDeleted = true;
                assessmentRow.DeletedAt = DateTime.UtcNow;
                byAssessmentId[incomingRow.ExternalAssessmentId] = row!;
            }

            if (!hasExistingKey)
            {
                row = new TaxonAssessment
                {
                    ScientificNameId = resolvedTaxon.ScientificNameId,
                    TaxonId = resolvedTaxon.TaxonId,
                    CategoryTypeId = categoryTypeId,
                    Area = incomingRow.Area,
                    Source = source
                };
                context.Set<TaxonAssessment>().Add(row);
                existing.Add(row);
                byKey.Add((row.TaxonId, row.Area, source), row);
                byAssessmentId.Add(row.ExternalAssessmentId = incomingRow.ExternalAssessmentId, row);
            }

            row!.ScientificNameId = resolvedTaxon.ScientificNameId;
            row.TaxonId = resolvedTaxon.TaxonId;
            row.CategoryId = categoryIds[incomingRow.CategoryCode];
            row.ExternalAssessmentId = incomingRow.ExternalAssessmentId;
            row.AssessmentUrl = source == TaxonAssessmentSource.Current
                ? BuildAssessmentUrl(incomingRow.ExternalAssessmentId)
                : BuildSnapshot2023AssessmentUrl(incomingRow.ExternalAssessmentId);
            row.LastSeenAt = DateTime.UtcNow;
            row.IsRetained = false;
            byAssessmentId[incomingRow.ExternalAssessmentId] = row;
            applied++;
        }

        return (applied, skipped);
    }

    private string BuildAssessmentUrl(string externalAssessmentId) =>
        $"{_options.CurrentAssessmentUrlPrefix.TrimEnd('/')}/{Uri.EscapeDataString(externalAssessmentId)}";

    private string BuildSnapshot2023AssessmentUrl(string externalAssessmentId) =>
        $"{_options.Snapshot2023AssessmentUrlPrefix.TrimEnd('/')}/{Uri.EscapeDataString(externalAssessmentId)}";

    private async Task ProjectObservationIndexCategoriesAsync(
        int categoryTypeId,
        IReadOnlyCollection<int> changedTaxonIds,
        IReadOnlyCollection<int> effectiveTaxonIds,
        CancellationToken cancellationToken)
    {
        var changedTaxonJson = JsonSerializer.Serialize(changedTaxonIds);
        var effectiveTaxonJson = JsonSerializer.Serialize(effectiveTaxonIds);

        while (changedTaxonIds.Count > 0 &&
               await InvalidateNextIndexBatchAsync(
                   categoryTypeId, changedTaxonJson, cancellationToken) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        int rows;
        do
        {
            rows = await ZeroNextIndexBatchAsync(
                categoryTypeId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        } while (rows > 0);

        do
        {
            rows = await ProjectNextIndexBatchAsync(
                categoryTypeId, effectiveTaxonJson, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        } while (rows > 0);
    }

    private Task<int> ZeroNextIndexBatchAsync(
        int categoryTypeId,
        CancellationToken cancellationToken) =>
        ExecuteIndexSqlAsync(
            """
            ;WITH Batch AS
            (
                SELECT TOP (@ZeroBatchSize)
                       idx.ObservationId,
                       idx.EntityTypeId,
                       idx.EntityId
                FROM dbo.ObservationEntityIndex idx
                WHERE idx.AlienSpeciesCategoryId IS NULL
                ORDER BY idx.ObservationId, idx.EntityTypeId, idx.EntityId
            )
            UPDATE idx
            SET idx.AlienSpeciesCategoryId = 0
            FROM dbo.ObservationEntityIndex idx
            INNER JOIN Batch batch
              ON batch.ObservationId = idx.ObservationId
             AND batch.EntityTypeId = idx.EntityTypeId
             AND batch.EntityId = idx.EntityId
            OPTION (RECOMPILE);
            """,
            categoryTypeId,
            "[]",
            cancellationToken);

    private Task UpdateAlienCategoryObservationCountsAsync(
        int categoryTypeId,
        CancellationToken cancellationToken)
    {
        return ExecuteIndexSqlAsync(
            """
            ;WITH CategoryCount AS
            (
                SELECT idx.AlienSpeciesCategoryId AS CategoryId,
                       COUNT_BIG(DISTINCT idx.ObservationId) AS ObservationCount
                FROM dbo.ObservationEntityIndex idx
                INNER JOIN dbo.Observation observation
                  ON observation.Id = idx.ObservationId
                WHERE idx.AlienSpeciesCategoryId > 0
                  AND idx.HiddenByAnnotation = 0
                  AND observation.IsDeleted = 0
                GROUP BY idx.AlienSpeciesCategoryId
            )
            UPDATE category
            SET category.ObservationCount = COALESCE(counts.ObservationCount, 0),
                category.UpdatedAt = SYSUTCDATETIME()
            FROM dbo.Category category
            LEFT JOIN CategoryCount counts ON counts.CategoryId = category.Id
            WHERE category.CategoryTypeId = @CategoryTypeId
              AND category.IsDeleted = 0;
            """,
            categoryTypeId,
            "[]",
            cancellationToken);
    }

    private Task<int> InvalidateNextIndexBatchAsync(
        int categoryTypeId,
        string taxonJson,
        CancellationToken cancellationToken) =>
        ExecuteIndexSqlAsync(
            """
            ;WITH Batch AS
            (
                SELECT TOP (@BatchSize) idx.ObservationId, idx.EntityTypeId, idx.EntityId
                FROM dbo.ObservationEntityIndex idx
                INNER JOIN dbo.Observation observation ON observation.Id = idx.ObservationId
                WHERE idx.AlienSpeciesCategoryId IS NOT NULL
                  AND observation.TaxonId IN
                  (
                      SELECT TRY_CONVERT(int, value)
                      FROM OPENJSON(@TaxonIds)
                  )
                ORDER BY idx.ObservationId, idx.EntityTypeId, idx.EntityId
            )
            UPDATE idx
            SET idx.AlienSpeciesCategoryId = NULL
            FROM dbo.ObservationEntityIndex idx
            INNER JOIN Batch batch
              ON batch.ObservationId = idx.ObservationId
             AND batch.EntityTypeId = idx.EntityTypeId
             AND batch.EntityId = idx.EntityId
            OPTION (RECOMPILE);
            """,
            categoryTypeId,
            taxonJson,
            cancellationToken);

    private Task<int> ProjectNextIndexBatchAsync(
        int categoryTypeId,
        string taxonJson,
        CancellationToken cancellationToken) =>
        ExecuteIndexSqlAsync(
            """
            ;WITH Batch AS
            (
                SELECT TOP (@BatchSize)
                       idx.ObservationId,
                       idx.EntityTypeId,
                       idx.EntityId,
                       COALESCE(
                           CASE WHEN region.IsSvalbard = 1 THEN svalbard.CategoryId END,
                           norway.CategoryId,
                           0) AS AlienSpeciesCategoryId
                FROM dbo.ObservationEntityIndex idx
                INNER JOIN dbo.Observation observation ON observation.Id = idx.ObservationId
                INNER JOIN OPENJSON(@TaxonIds) assessedTaxon
                  ON TRY_CONVERT(int, assessedTaxon.value) = observation.TaxonId
                CROSS APPLY
                (
                    SELECT CASE WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.LocationAreas locationArea
                        INNER JOIN dbo.Area area ON area.Id = locationArea.AreaId
                        WHERE locationArea.LocationId = observation.LocationId
                          AND area.IsCurrent = 1
                          AND area.IsDeleted = 0
                          AND (
                              area.AreaTypeId = 6 OR
                              (area.AreaTypeId = 2 AND area.Fid = N'21')
                          )
                    ) THEN 1 ELSE 0 END AS IsSvalbard
                ) region
                LEFT JOIN dbo.TaxonAssessment svalbard
                  ON svalbard.TaxonId = observation.TaxonId
                 AND svalbard.CategoryTypeId = @CategoryTypeId
                 AND svalbard.Area = N'S'
                 AND svalbard.IsEffective = 1
                 AND svalbard.IsDeleted = 0
                LEFT JOIN dbo.TaxonAssessment norway
                  ON norway.TaxonId = observation.TaxonId
                 AND norway.CategoryTypeId = @CategoryTypeId
                 AND norway.Area = N'N'
                 AND norway.IsEffective = 1
                 AND norway.IsDeleted = 0
                WHERE idx.AlienSpeciesCategoryId = 0
                  AND COALESCE(
                      CASE WHEN region.IsSvalbard = 1 THEN svalbard.CategoryId END,
                      norway.CategoryId) IS NOT NULL
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.TaxonAssessment assessment
                      WHERE assessment.TaxonId = observation.TaxonId
                        AND assessment.CategoryTypeId = @CategoryTypeId
                        AND assessment.IsDeleted = 0
                        AND assessment.IsEffective = 1
                  )
                ORDER BY idx.ObservationId, idx.EntityTypeId, idx.EntityId
            )
            UPDATE idx
            SET idx.AlienSpeciesCategoryId = batch.AlienSpeciesCategoryId
            FROM dbo.ObservationEntityIndex idx
            INNER JOIN Batch batch
              ON batch.ObservationId = idx.ObservationId
             AND batch.EntityTypeId = idx.EntityTypeId
             AND batch.EntityId = idx.EntityId
            OPTION (RECOMPILE);
            """,
            categoryTypeId,
            taxonJson,
            cancellationToken);

    private async Task<int> ExecuteIndexSqlAsync(
        string sql,
        int categoryTypeId,
        string taxonJson,
        CancellationToken cancellationToken)
    {
        var previousTimeout = context.Database.GetCommandTimeout();
        context.Database.SetCommandTimeout(_options.IndexProjectionCommandTimeoutSeconds);
        try
        {
            return await context.Database.ExecuteSqlRawAsync(
                sql,
                [
                    new SqlParameter("@CategoryTypeId", categoryTypeId),
                    new SqlParameter("@TaxonIds", SqlDbType.NVarChar, -1) { Value = taxonJson },
                    new SqlParameter(
                        "@BatchSize",
                        Math.Max(1, _options.IndexProjectionBatchSize)),
                    new SqlParameter(
                        "@ZeroBatchSize",
                        Math.Max(1, _options.IndexZeroProjectionBatchSize))
                ],
                cancellationToken);
        }
        finally
        {
            context.Database.SetCommandTimeout(previousTimeout);
        }
    }

    private static int[] GetChangedTaxonIds(
        Dictionary<(int TaxonId, string Area), int?> before,
        Dictionary<(int TaxonId, string Area), int?> after) =>
        before.Keys
            .Concat(after.Keys)
            .Select(key => key.TaxonId)
            .Distinct()
            .Where(taxonId =>
                !before
                    .Where(pair => pair.Key.TaxonId == taxonId)
                    .OrderBy(pair => pair.Key.Area)
                    .Select(pair => $"{pair.Key.Area}:{pair.Value}")
                    .SequenceEqual(
                        after
                            .Where(pair => pair.Key.TaxonId == taxonId)
                            .OrderBy(pair => pair.Key.Area)
                            .Select(pair => $"{pair.Key.Area}:{pair.Value}")))
            .ToArray();

    private static Dictionary<(int TaxonId, string Area), int?> GetEffectiveProjection(
        IEnumerable<TaxonAssessment> assessments) =>
        assessments
            .Where(a => a.IsEffective && !a.IsDeleted)
            .ToDictionary(a => (a.TaxonId, a.Area), a => (int?)a.CategoryId);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record ResolvedAssessmentTaxon(int TaxonId, int ScientificNameId);
}
