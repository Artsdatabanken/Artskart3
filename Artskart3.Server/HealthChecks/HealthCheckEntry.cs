using System.Text.Json.Serialization;

namespace Artskart3.Server.HealthChecks;
public class HealthCheckEntry
{
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
