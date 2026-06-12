using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;

public class AreaModel
{
    public int Id { get; set; }
    public AreaType Type { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    [JsonIgnore]
    public DateTime TimeStamp { get; set; }
    [JsonIgnore]
    public Geometry? Geometry { get; set; }

    public AreaModel() { }

    public AreaModel(int id, AreaType type, string code, string name, string wkt, int epsg)
    {
        Id = id;
        Type = type;
        Code = code;
        Name = name;
        var wktReader = new WKTReader();
        var geometry = wktReader.Read(wkt);
        geometry.SRID = epsg;
        Geometry = geometry;
    }
}