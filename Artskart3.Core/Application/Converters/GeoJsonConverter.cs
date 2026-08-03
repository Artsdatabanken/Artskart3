using System.Text;
using System.Text.Json;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Converters;

public static class GeoJsonConverter
{
    private const int DefaultEpsg = 25833;

    /// <summary>
    /// Serialiserer lokasjoner til kompakt JSON-format: { epsg, locations: [[id, lon, lat, count, "locality"], ...] }
    /// </summary>
    public static async Task<string> LocationsToCompactJson(
        IAsyncEnumerable<LocationModel> locations,
        int? targetEpsg = null,
        CancellationToken cancellationToken = default)
    {
        int epsgCode = targetEpsg ?? DefaultEpsg;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("epsg", epsgCode);

        writer.WritePropertyName("locations");
        writer.WriteStartArray();

        await foreach (var location in locations.WithCancellation(cancellationToken))
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(location.Id);
            writer.WriteNumberValue(location.Longitude);
            writer.WriteNumberValue(location.Latitude);
            writer.WriteNumberValue(location.ObservationCount);
            writer.WriteStringValue(location.Locality ?? string.Empty);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
