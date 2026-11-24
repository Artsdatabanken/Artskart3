using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

public class TaxonAssessment : BaseEntity
{
    public int ExternalTaxonId { get; set; }

    public int TaxonId { get; set; }

    public int CategoryTypeId { get; set; }

    public int CategoryId { get; set; }

    public string Area { get; set; } = null!;

    public string ExternalAssessmentId { get; set; } = null!;

    public TaxonAssessmentSource Source { get; set; }

    public string? AssessmentUrl { get; set; }

    public DateTime LastSeenAt { get; set; }

    public bool IsEffective { get; set; }

    public bool IsRetained { get; set; }

    public virtual Taxon Taxon { get; set; } = null!;

    public virtual CategoryType CategoryType { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;
}
