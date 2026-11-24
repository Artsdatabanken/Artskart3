using Artskart3.Core.Domain.Enums;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public static class AlienSpeciesAssessmentNormalizer
{
    public static IReadOnlyList<NormalizedTaxonAssessment> NormalizeCurrent(
        IEnumerable<CurrentAlienSpeciesAssessment> assessments)
    {
        var result = assessments.Select(assessment =>
            Normalize(
                assessment.TaxonId,
                assessment.Area,
                assessment.Category,
                assessment.Id.ToString(),
                TaxonAssessmentSource.Current)).ToArray();
        EnsureUniqueKeys(result);
        return result;
    }

    public static IReadOnlyList<NormalizedTaxonAssessment> NormalizeSnapshot2023(
        IEnumerable<AlienSpeciesAssessment2023> assessments)
    {
        var result = assessments.Select(assessment =>
            Normalize(
                assessment.ScientificName?.ScientificNameId ?? 0,
                assessment.EvaluationContext,
                assessment.Category,
                assessment.Id.ToString(),
                TaxonAssessmentSource.Snapshot2023)).ToArray();
        EnsureUniqueKeys(result);
        return result;
    }

    private static NormalizedTaxonAssessment Normalize(
        int taxonId,
        string area,
        string categoryCode,
        string assessmentId,
        TaxonAssessmentSource source)
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
            source);
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
}
