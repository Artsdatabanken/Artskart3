using System.Text;
using System.Text.Json;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Converters;

public static class GeoJsonConverter
{
    private const int DefaultEpsg = 25833;

    /// <summary>
    /// Serialiserer lokasjoner til kompakt JSON-format: { epsg, locations: [[id, lon, lat, count], ...] }
    /// </summary>
    public static string LocationsToCompactJson(
        List<LocationModel> locations,
        int? targetEpsg = null)
    {
        int epsgCode = targetEpsg ?? DefaultEpsg;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("epsg", epsgCode);

        writer.WritePropertyName("locations");
        writer.WriteStartArray();

        foreach (var location in locations)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(location.Id);
            writer.WriteNumberValue(location.Longitude);
            writer.WriteNumberValue(location.Latitude);
            writer.WriteNumberValue(location.ObservationCount);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
