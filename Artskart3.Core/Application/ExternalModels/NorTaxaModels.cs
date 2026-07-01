namespace Artskart3.Core.Application.ExternalModels;

/// <summary>
/// Modeller for deserialisering av NorTaxa Search API-respons.
/// </summary>
public class NorTaxaSearchResult
{
    public int TaxonId { get; set; }
    public NorTaxaAcceptedScientificName AcceptedScientificName { get; set; } = new();
    public List<NorTaxaVernacularName> PreferredVernacularNames { get; set; } = [];
    public List<NorTaxaScientificNameSynonym> ScientificNameSynonyms { get; set; } = [];
    public List<NorTaxaVernacularName> VernacularNameSynonyms { get; set; } = [];
}

public class NorTaxaAcceptedScientificName
{
    public int NameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameFormatted { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string TaxonomicStatus { get; set; } = string.Empty;
}

public class NorTaxaVernacularName
{
    public string Name { get; set; } = string.Empty;
    public string NameLanguageIso { get; set; } = string.Empty;
}

public class NorTaxaScientificNameSynonym
{
    public int NameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameFormatted { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string TaxonomicStatus { get; set; } = string.Empty;
}

/// <summary>
/// Modeller for deserialisering av NorTaxa ByTaxonId API-respons.
/// </summary>
public class NorTaxaByTaxonIdResult
{
    public int TaxonId { get; set; }
    public List<NorTaxaScientificName> ScientificNames { get; set; } = [];
    public List<NorTaxaDetailedVernacularName> VernacularNames { get; set; } = [];
}

public class NorTaxaScientificName
{
    public int Id { get; set; }
    public int TaxonId { get; set; }
    public string TaxonomicStatus { get; set; } = string.Empty;
    public string ScientificNamePresentation { get; set; } = string.Empty;
    public string ScientificNamePresentationHtml { get; set; } = string.Empty;
    public string ScientificNameAuthorship { get; set; } = string.Empty;
    public string TaxonRank { get; set; } = string.Empty;
}

public class NorTaxaDetailedVernacularName
{
    public int Id { get; set; }
    public int TaxonId { get; set; }
    public string VernacularName { get; set; } = string.Empty;
    public string VernacularNameStatus { get; set; } = string.Empty;
    public string LanguageIsoCode { get; set; } = string.Empty;
}
