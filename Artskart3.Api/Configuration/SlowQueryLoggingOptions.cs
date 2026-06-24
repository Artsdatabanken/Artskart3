namespace Artskart3.Api.Configuration;

public class SlowQueryLoggingOptions
{
    public const string SectionName = "SlowQueryLogging";

    public bool Enabled { get; set; } = true;
    public long ThresholdMs { get; set; } = 1000;
}
