using System;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.Entities;

public partial class SpatialRefSy
{
    public int Srid { get; set; }

    public string? AuthName { get; set; }

    public int? AuthSrid { get; set; }

    public string? Srtext { get; set; }

    public string? Proj4text { get; set; }
}
