using System.Data.SqlTypes;
using Newtonsoft.Json;
using Microsoft.SqlServer.Types;
using System.Collections.ObjectModel;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;
public class LocationModel
{
    public int Id { get; set; }
    [JsonIgnore]
    public SqlGeometry Geometry { get; set; }
    public string Locality { get; set; }
    public int? CoordinatePrecision { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int East { get; set; }
    public int North { get; set; }
    public Collection<ObservationBaseModel> Observations { get; set; }
    public Collection<AreaModel> Areas { get; set; }

    public LocationModel(int id, string wkt, int epsg, string locality, int? coordinatePrecision, double latitude, double longitude)
    {
            Id = id;
            Geometry = SqlGeometry.STGeomFromText(new SqlChars(wkt), epsg);
            Locality = locality;
            CoordinatePrecision = coordinatePrecision;
            Latitude = latitude;
            Longitude = longitude;
    }

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
