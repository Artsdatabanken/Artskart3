using System.Data.SqlTypes;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;
public class LocationModel
{
    public int Id { get; set; }
    [JsonIgnore]
    public Geometry? Geometry { get; set; }
    public string Locality { get; set; } = string.Empty;
    public int? CoordinatePrecision { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int East { get; set; }
    public int North { get; set; }
    public Collection<ObservationBaseModel> Observations { get; set; } = new Collection<ObservationBaseModel>();
    public Collection<AreaModel> Areas { get; set; } = new Collection<AreaModel>();
    public LocationModel()
    {
        Observations = new Collection<ObservationBaseModel>();
        Areas = new Collection<AreaModel>();
    }

    private int _observationCount;
    [JsonIgnore]
    public int ObservationCount
    {
        get
        {
            if (Observations.Count != 0)
            {
                _observationCount = Observations.Count;
                return _observationCount;
            }
            return _observationCount;
        }

        set { _observationCount = value; }
    }

    private Category _maxCategory;
    [JsonIgnore]
    public Category MaxCategory
    {
        get
        {
            if (Observations.Count != 0)
            {
                _maxCategory = Category.Unknown;
                foreach (var observation in Observations)
                {
                    if (observation.GetType() == typeof (ObservationModel) || observation.GetType() == typeof(ObservationWithLocationModel))
                    {
                        if (((ObservationModel) observation).Category > _maxCategory)
                        {
                            _maxCategory = ((ObservationModel) observation).Category;
                        }
                    }
                    else
                    {
                        throw new Exception("Cast of observation in Location.MaxCategory failed");
                    }
                }
            }
            return _maxCategory;
        }

        set { _maxCategory = value; }
    }

    public int DominantTaxonId { get; set; }

    [JsonIgnore]
    public int X { get; set; }
    [JsonIgnore]
    public int Y { get; set; }
}
