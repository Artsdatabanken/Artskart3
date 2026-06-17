using System.Text;
using System.Text.Json;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Converters;

public static class GeoJsonConverter
{
    private const int DefaultEpsg = 25833;

    public static async Task<string> LocationsToGeoJson(
        IAsyncEnumerable<LocationModel> locations,
        StyleType styleType = StyleType.Unknown,
        int? targetEpsg = null,
        CancellationToken cancellationToken = default)
    {
        int epsgCode = targetEpsg ?? DefaultEpsg;
        var features = new List<JsonElement>();

        await foreach (var location in locations.WithCancellation(cancellationToken))
        {
            var feature = CreateFeatureJson(location, styleType, epsgCode);
            features.Add(feature);
        }

        var featureCollection = CreateFeatureCollection(features, epsgCode);
        return featureCollection;
    }

    private static JsonElement CreateFeatureJson(LocationModel location, StyleType styleType, int epsgCode)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "Feature");
        writer.WriteString("id", location.Id.ToString());

        // Write geometry
        writer.WritePropertyName("geometry");
        writer.WriteStartObject();
        writer.WriteString("type", "Point");
        writer.WritePropertyName("coordinates");
        writer.WriteStartArray();
        writer.WriteNumberValue(location.Longitude);
        writer.WriteNumberValue(location.Latitude);
        writer.WriteEndArray();
        writer.WriteEndObject();

        // Write properties
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteNumber("ObservationCount", location.ObservationCount);
        writer.WriteString("Locality", location.Locality ?? string.Empty);

        switch (styleType)
        {
            case StyleType.Category:
                writer.WriteNumber("MaxCategory", (int)location.MaxCategory);
                break;
            case StyleType.Precision:
                if (location.CoordinatePrecision.HasValue)
                {
                    writer.WriteNumber("Precision", location.CoordinatePrecision.Value);
                }
                break;
            case StyleType.Species:
                if (location.DominantTaxonId > 0)
                {
                    writer.WriteNumber("TaxonId", location.DominantTaxonId);
                }
                break;
        }
        writer.WriteEndObject();

        // Write CRS
        WriteCrs(writer, epsgCode);

        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string CreateFeatureCollection(List<JsonElement> features, int epsgCode)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");

        writer.WritePropertyName("features");
        writer.WriteStartArray();
        foreach (var feature in features)
        {
            feature.WriteTo(writer);
        }
        writer.WriteEndArray();

        // Write CRS
        WriteCrs(writer, epsgCode);

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCrs(Utf8JsonWriter writer, int epsg)
    {
        writer.WritePropertyName("crs");
        writer.WriteStartObject();

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("name", $"EPSG:{epsg}");
        writer.WriteEndObject();

        writer.WriteString("type", "Name");

        writer.WriteEndObject();
    }
}
