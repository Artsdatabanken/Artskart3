using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Organization : BaseEntity
{
    public int? ParentId { get; set; }

    public string? ExternalId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int OrganizationTypeId { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }

    public int? ObservationCount { get; set; }

    /// <summary>
    /// DwC-<c>datasetID</c> slik kilden oppgir den, for datasett-organisasjoner (type 3).
    ///
    /// EGEN KOLONNE, IKKE GJENBRUK AV <see cref="ExternalId"/>: den peker på
    /// RavenDB-dokumentet raden kom fra og beholdes uendret som sporbarhet. Uten denne
    /// kolonnen faller importen tilbake på oppslag på datasettnavn, og to datasett med
    /// samme navn under samme samling smelter sammen til ett.
    /// </summary>
    public string? SourceDatasetId { get; set; }

    public virtual ICollection<Organization> InverseParent { get; set; } = new List<Organization>();

    public virtual OrganizationType OrganizationType { get; set; } = null!;

    public virtual Organization? Parent { get; set; }

    public virtual ICollection<OrganizationAlias> Aliases { get; set; } = new List<OrganizationAlias>();
}
