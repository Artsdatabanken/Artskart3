using System;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.Entities;

public partial class Maskeringsruter8x8km 
{
    public int Objectid { get; set; }

    public int? KriteriumMaskeringsrute { get; set; }

    public string? Ruteid { get; set; }
}
