namespace Artskart3.Core.Application.DTOs
{
    public class BasisOfRecordDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string Variants { get; set; } = null!;
        public int? ObservationCount { get; set; }
    }
}
