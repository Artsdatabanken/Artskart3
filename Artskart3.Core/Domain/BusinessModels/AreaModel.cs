using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using Artskart3.Core.Domain.Enums;
using System.Data.SqlTypes;

namespace Artskart3.Core.Domain.BusinessModels;

public class AreaModel
{
    public int Id { get; set; }
    public AreaType Type { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    [JsonIgnore]
    public DateTime TimeStamp { get; set; }
    [JsonIgnore]
    public SqlGeometry Geometry { get; set; }

    public AreaModel() { }

    public AreaModel(int id, AreaType type, string code, string name, string wkt, int epsg)
    {
        Id = id;
        Type = type;
        Code = code;
        Name = name;
        Geometry = SqlGeometry.STGeomFromText(new SqlChars(wkt), epsg);
    }
}