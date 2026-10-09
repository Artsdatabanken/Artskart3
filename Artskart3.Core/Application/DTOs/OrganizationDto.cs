namespace Artskart3.Core.Application.DTOs;

public class OrganizationDto
{    public int Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>
    /// Totalt antall observasjoner, uavhengig av filteret. Satt bare i typeaheaden for datasett og prosjekt.
    /// </summary>
    public int? ObservationCount { get; set; }
}
