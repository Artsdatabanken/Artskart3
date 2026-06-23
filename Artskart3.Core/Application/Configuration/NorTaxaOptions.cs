namespace Artskart3.Core.Application.Configuration;

public class NorTaxaOptions
{
    public const string SectionName = "NorTaxa";

    public string BaseUrl { get; set; } = "https://nortaxa.artsdatabanken.no/";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 20;
}
