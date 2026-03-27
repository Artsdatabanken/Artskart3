using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class GeometryColumn : BaseEntity
{
    public string FTableCatalog { get; set; } = null!;

    public string FTableSchema { get; set; } = null!;

    public string FTableName { get; set; } = null!;

    public string FGeometryColumn { get; set; } = null!;

    public int CoordDimension { get; set; }

    public int Srid { get; set; }

    public string GeometryType { get; set; } = null!;
}
