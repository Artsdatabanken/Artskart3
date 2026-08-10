namespace Artskart3.Core.Application.DTOs;

public class ObservationListInfoDto
{
    public int Id { get; set; }
    public string? PreferredPopularName { get; set; }
    public string? ScientificName { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? Author { get; set; }
    public int? TaxonGroupId { get; set; }
    public string? TaxonGroupName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
    public string? Locality { get; set; }
    public IEnumerable<string>? RegistrationType { get; set; }
    public string? IdentifiedBy { get; set; }
}
