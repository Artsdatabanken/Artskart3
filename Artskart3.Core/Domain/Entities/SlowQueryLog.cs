namespace Artskart3.Core.Domain.Entities;

public class SlowQueryLog
{
    public int Id { get; set; }
    public string Endpoint { get; set; } = null!;
    public long QueryTimeMs { get; set; }
    public long ThresholdMs { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestBody { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
