namespace Artskart3.Core.Application.DTOs;

public class AreaCountDto
{
    public string Fid { get; set; } = null!;
    public int ObservationCount { get; set; }
}

public record AreaCountsResultDto(AreaCountDto[] Counts, string Etag);
