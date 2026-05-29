namespace Artskart3.Core.Application.DTOs
{
    public class InstitutionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public int? ObservationCount { get; set; }
    }
}
