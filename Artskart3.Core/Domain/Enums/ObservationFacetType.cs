using System;
using System.Collections.Generic;
using System.Text;

namespace Artskart3.Core.Domain.Enums
{
    public enum ObservationFacetType
    {
        Category = 1,
        BasisOfRecord = 2,
        Institution = 3,
        Behavior = 4,
        Tag = 5,
        Month = 6,
        Taxon = 7,
        TaxonGroup = 8,
        DistinctTaxonsPerInstitution = 9,
        Year = 10,
        Decade = 11,

        Img = 20,
        Spontan = 21,
        Found = 22,
        Valid = 23,
        UnsureId = 24,
        NotRecovered = 25,
        Errors = 26,
        Blocked = 27,
        Sensitive = 28,
        AnnotatedChanges = 29
    }
}
