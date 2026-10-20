using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;

public class LocationModel
{
    public int Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ObservationCount { get; set; }

    /// <summary>
    /// Found hvis minst ett funn på lokaliteten matcher filteret. Ellers NotRecovered hvis noen
    /// er ikke gjenfunnet, og Absent når alle er ikke funnet.
    /// </summary>
    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Found;
}
