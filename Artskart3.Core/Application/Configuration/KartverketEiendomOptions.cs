namespace Artskart3.Core.Application.Configuration;

public class KartverketEiendomOptions
{
    public const string SectionName = "KartverketEiendom";

    public string BaseUrl { get; set; } = "https://api.kartverket.no/eiendom/v1/";
    public int TimeoutSeconds { get; set; } = 10;
}
