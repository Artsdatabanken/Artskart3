namespace Artskart3.Core.Application.DTOs;

public class PagedObservationImageResponseDto
{
    public IEnumerable<ObservationImageDto>? Items { get; set; }

    public int PageNumber { get; set; }

    public int ResultsPerPage { get; set; }

    public int LookaheadCount { get; set; }

    public bool HasMorePages { get; set; }
}
