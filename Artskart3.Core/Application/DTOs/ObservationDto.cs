namespace Artskart3.Core.Application.DTOs
{
    public record ObservationDto
    {
        public int Id { get; set; }

        public string? PreferredPopularName { get; set; }

        public string? ScientificName { get; set; }

        public string? Author { get; set; }

        public string? Institution { get; set; }

        public string? Locality { get; set; }

        public string? MunicipalityId { get; set; }

        public int? TaxonGroupId { get; set; }

        public int? CategoryId { get; set; }

        public DateTime? DateTimeCollected { get; set; }

    }
}
