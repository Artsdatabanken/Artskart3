namespace Artskart3.Core.Domain.BusinessModels;

public class LocationModel
{
    public int Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ObservationCount { get; set; }
}
