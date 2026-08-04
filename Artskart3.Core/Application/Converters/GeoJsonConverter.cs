using System.Text.Json;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Converters;

public static class GeoJsonConverter
{
    private const int DefaultEpsg = 25833;

    /// <summary>
    /// Skriver lokasjoner direkte til en strøm i kompakt JSON-format: { epsg, locations: [[id, lon, lat, count], ...] }
    /// </summary>
    public static async Task WriteLocationsToStreamAsync(
        Stream output,
        List<LocationModel> locations,
        int? targetEpsg = null)
    {
        int epsgCode = targetEpsg ?? DefaultEpsg;

        await using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = false });

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
        await writer.FlushAsync();
    }
}
