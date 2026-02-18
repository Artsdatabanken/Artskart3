using System.Text.Json.Serialization;

namespace Artskart3.Server.HealthChecks;
public class HealthCheckResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("totalDuration")]
    public TimeSpan TotalDuration { get; set; }

    [JsonPropertyName("entries")]
    public Dictionary<string, HealthCheckEntry> Entries { get; set; } = new();
}
