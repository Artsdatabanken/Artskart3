namespace Artskart3.Core.Application.DTOs
{
    public abstract class PaginatedRequestDto
    {
        public int? PageNumber { get; set; }

        public int? ResultsPerPage { get; set; }

        public bool IsPaginated => PageNumber.HasValue && ResultsPerPage.HasValue;
    }
}
