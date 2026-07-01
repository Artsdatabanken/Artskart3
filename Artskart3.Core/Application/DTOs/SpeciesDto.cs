namespace Artskart3.Core.Application.DTOs;

public class SpeciesDto
{
    public int TaxonId { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string ScientificNameFormatted { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public List<VernacularNameDto> PreferredVernacularNames { get; set; } = [];
    public List<VernacularNameDto> VernacularNameSynonyms { get; set; } = [];
    public List<ScientificNameSynonymDto> ScientificNameSynonyms { get; set; } = [];
}

public class VernacularNameDto
{
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

public class ScientificNameSynonymDto
{
    public string Name { get; set; } = string.Empty;
    public string NameFormatted { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
}
