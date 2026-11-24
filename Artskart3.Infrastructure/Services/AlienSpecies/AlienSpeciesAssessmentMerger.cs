using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public static class AlienSpeciesAssessmentMerger
{
    public static void SelectEffective(IEnumerable<TaxonAssessment> assessments)
    {
        var all = assessments.ToArray();
        foreach (var assessment in all)
            assessment.IsEffective = false;

        foreach (var group in all
                     .Where(assessment => !assessment.IsDeleted)
                     .GroupBy(a => (a.TaxonId, a.CategoryTypeId, a.Area)))
        {
            var effective = group
                .OrderByDescending(a => a.Source == TaxonAssessmentSource.Current)
                .ThenBy(a => a.IsRetained)
                .ThenByDescending(a => a.LastSeenAt)
                .FirstOrDefault();

            if (effective is not null)
                effective.IsEffective = true;
        }
    }
}
