using System;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.Entities;

public partial class ViewArtslisteTilMd
{
    public int TaxonId { get; set; }

    public int TaxonRankId { get; set; }

    public int ValidScientificNameId { get; set; }

    public string? ValidScientificName { get; set; }

    public string? ValidScientificNameAuthorship { get; set; }

    public string? PrefferedPopularname { get; set; }

    public int? CumulativeObservationCount { get; set; }

    public string TagGroup { get; set; } = null!;

    public string Tag { get; set; } = null!;

    public string TaxonGroupName { get; set; } = null!;

    public string Grunnlag { get; set; } = null!;
}
