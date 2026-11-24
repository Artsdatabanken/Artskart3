using Artskart3.Core.Domain.Enums;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public static class AlienSpeciesAssessmentNormalizer
{
    public static IReadOnlyList<NormalizedTaxonAssessment> NormalizeCurrent(
        IEnumerable<CurrentAlienSpeciesAssessment> assessments)
    {
        var result = assessments.Select(assessment =>
            Normalize(
                assessment.ScientificNameInformation.NameId,
                assessment.Area,
                assessment.Category,
                assessment.Id.ToString(),
                TaxonAssessmentSource.Current,
                assessment.PreviousAssessments?
                    .FirstOrDefault(previous => previous.RevisionYear == 2023)?
                    .AssessmentId)).ToArray();
        EnsureUniqueKeys(result);
        return result;
    }

    public static IReadOnlyList<NormalizedTaxonAssessment> NormalizeSnapshot2023(
        IEnumerable<AlienSpeciesAssessment2023> assessments)
    {
        var candidates = assessments
            .Select(assessment => Normalize(
                assessment.ScientificName?.ScientificNameId ?? 0,
                assessment.EvaluationContext,
                assessment.Category,
                assessment.Id.ToString(),
                TaxonAssessmentSource.Snapshot2023))
            .ToArray();
        var result = candidates
            .GroupBy(candidate => (
                candidate.SourceTaxonIdentifier,
                candidate.Area))
            .Select(group => SelectSnapshotAssessment(group.ToArray()))
            .ToArray();
        EnsureUniqueKeys(result);
        return result;
    }

    private static NormalizedTaxonAssessment Normalize(
        int taxonId,
        string area,
        string categoryCode,
        string assessmentId,
        TaxonAssessmentSource source,
        string? previous2023AssessmentId = null)
    {
        if (taxonId <= 0)
            throw new InvalidDataException($"Assessment {assessmentId} has no valid taxon ID.");

        var normalizedArea = area.Trim().ToUpperInvariant();
        if (normalizedArea is not ("N" or "S"))
            throw new InvalidDataException($"Assessment {assessmentId} has unsupported area '{area}'.");

        var normalizedCode = categoryCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length is 0 or > 2)
            throw new InvalidDataException($"Assessment {assessmentId} has invalid category '{categoryCode}'.");

        return new NormalizedTaxonAssessment(
            taxonId,
            normalizedArea,
            normalizedCode,
            assessmentId,
            source,
            previous2023AssessmentId);
    }

    private static void EnsureUniqueKeys(IEnumerable<NormalizedTaxonAssessment> assessments)
    {
        var duplicate = assessments
            .GroupBy(a => new { a.SourceTaxonIdentifier, a.Area, a.Source })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Multiple {duplicate.Key.Source} assessments exist for identifier {duplicate.Key.SourceTaxonIdentifier} and area {duplicate.Key.Area}.");
        }
    }

    private static NormalizedTaxonAssessment SelectSnapshotAssessment(
        NormalizedTaxonAssessment[] candidates)
    {
        if (candidates.Length == 1)
            return candidates[0];

        return candidates
            .OrderByDescending(assessment => RiskCategoryRank(assessment.CategoryCode))
            .ThenByDescending(assessment => int.Parse(
                assessment.ExternalAssessmentId,
                System.Globalization.CultureInfo.InvariantCulture))
            .First();
    }

    private static int RiskCategoryRank(string categoryCode) =>
        Enum.TryParse<AlienSpeciesStatus>(categoryCode, ignoreCase: true, out var status) &&
        status != AlienSpeciesStatus.Unknown
            ? (int)status
            : throw new InvalidDataException(
                $"Unsupported alien-species category '{categoryCode}' in Snapshot2023.");
}
