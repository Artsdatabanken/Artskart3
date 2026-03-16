using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ViewShapeExport2 : BaseEntity
{
    public int NodeDatabaseId { get; set; }

    public int InstId { get; set; }

    public int CollId { get; set; }

    public DateTime DateLastModified { get; set; }

    public string? InstitutionCode { get; set; }

    public string? CollectionCode { get; set; }

    public string? CatalogNumber { get; set; }

    public string? VitNavn { get; set; }

    public string? BasisOfRecord { get; set; }

    public string? Kingdom { get; set; }

    public string? Phylum { get; set; }

    public string? Class { get; set; }

    public string? Order { get; set; }

    public string? Family { get; set; }

    public string? Genus { get; set; }

    public string? Species { get; set; }

    public string? Subspecies { get; set; }

    public string? Author { get; set; }

    public string? IdentifiedBy { get; set; }

    public int YearIdentified { get; set; }

    public int MonthIdentified { get; set; }

    public int DayIdentified { get; set; }

    public string? TypeStatus { get; set; }

    public string? CollectorNumber { get; set; }

    public string? FieldNumber { get; set; }

    public string? Collector { get; set; }

    public int YearCollected { get; set; }

    public int MonthCollected { get; set; }

    public int DayCollected { get; set; }

    public string ContinentOcean { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string? StateProvince { get; set; }

    public string? CountyOrg { get; set; }

    public string? Locality { get; set; }

    public string? CountyId { get; set; }

    public string? MunicipalityId { get; set; }

    public string? County { get; set; }

    public string? MuniName { get; set; }

    public double? Longitude { get; set; }

    public double? Latitude { get; set; }

    public int? CoordinatePrecision { get; set; }

    public string BoundingBox { get; set; } = null!;

    public int? MinElevation { get; set; }

    public int? MaxElevation { get; set; }

    public string MinDepth { get; set; } = null!;

    public string MaxDepth { get; set; } = null!;

    public string? Sex { get; set; }

    public string? PreparationType { get; set; }

    public string? IndividualCount { get; set; }

    public string? PreviousCatalogNumber { get; set; }

    public string? RelationshipType { get; set; }

    public string? RelatedCatalogItem { get; set; }

    public string? Notes { get; set; }

    public string? CollectingMethod { get; set; }

    public int IdentificationPrecision { get; set; }

    public string? NorskNavn { get; set; }

    public string Okologi { get; set; } = null!;

    public string? Habitat { get; set; }

    public string Substrat { get; set; } = null!;

    public int Utmost { get; set; }

    public int Utmnord { get; set; }

    public string Utmsone { get; set; } = null!;

    public string Mgrsfra { get; set; } = null!;

    public string Mgrstil { get; set; } = null!;

    public int Utm33nord { get; set; }

    public int Utm33ost { get; set; }

    public string? KoordKilde { get; set; }

    public string ElevationKilde { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string RelativeAboundance { get; set; } = null!;

    public int Antropokor { get; set; }

    public string? Url { get; set; }

    public int ArtsGruppe { get; set; }

    public string? Iname { get; set; }

    public string? Cname { get; set; }

    public int CategoryId { get; set; }

    public string? ProxyId { get; set; }
}
