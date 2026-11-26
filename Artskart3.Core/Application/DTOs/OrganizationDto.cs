namespace Artskart3.Core.Application.DTOs;

public class OrganizationDto
{    public int Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>
    /// Totalt antall observasjoner, uavhengig av filteret. Satt bare i typeaheaden for datasett og prosjekt.
    /// </summary>
    public int? ObservationCount { get; set; }

    /// <summary>
    /// 1 institusjon, 2 datasett, 3 prosjekt. Satt bare ved oppslag på id, der det avgjør hvilket
    /// filterfelt en gammel Artskart 2-lenke skal oversettes til.
    /// </summary>
    public int? OrganizationTypeId { get; set; }
}
