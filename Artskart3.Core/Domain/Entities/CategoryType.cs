using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class CategoryType : BaseEntity
{
    public string Name { get; set; } = null!;

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    public virtual ICollection<TaxonAssessment> TaxonAssessments { get; set; } = new List<TaxonAssessment>();

    public virtual ICollection<TaxonAssessmentSyncRun> TaxonAssessmentSyncRuns { get; set; } = new List<TaxonAssessmentSyncRun>();
}
