namespace Artskart3.Core.Application.DTOs
{
    public class PagedObservationResponseDto
    {
        public IEnumerable<ObservationDto>? Items { get; set; }

        public int PageNumber { get; set; }

        public int ResultsPerPage { get; set; }

        public int LookaheadCount { get; set; }
    }
}
