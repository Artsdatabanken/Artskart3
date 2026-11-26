namespace Artskart3.Core.Application.Configuration;

/// <summary>
/// Annoteringstjenesten fra Artskart 2. Alle miljøer bruker produksjonsinstansen, fordi det
/// er der annoteringene som er brukt på dataene våre ligger.
/// </summary>
public class AnnotationApiOptions
{
    public const string SectionName = "AnnotationApi";

    public string BaseUrl { get; set; } = "https://artskart2.artsdatabanken.no/annotationapi/";
    public int TimeoutSeconds { get; set; } = 10;
}
