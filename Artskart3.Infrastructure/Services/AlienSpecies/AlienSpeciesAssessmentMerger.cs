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

        foreach (var group in all.GroupBy(a => (a.TaxonId, a.CategoryTypeId, a.Area)))
        {
            var effective = group
                .FirstOrDefault(a => !a.IsRetained && a.Source == TaxonAssessmentSource.Current)
                ?? group.FirstOrDefault(a => !a.IsRetained && a.Source == TaxonAssessmentSource.Snapshot2023)
                ?? group
                    .OrderByDescending(a => a.LastSeenAt)
                    .ThenByDescending(a => a.Source == TaxonAssessmentSource.Current)
                    .FirstOrDefault();

            if (effective is not null)
                effective.IsEffective = true;
        }
    }
}
