namespace Artskart3.Core.Application.DTOs
{
    public class BehaviorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Variants { get; set; } = null!;
        public int? ObservationCount { get; set; }
        public string? Description { get; set; }
    }
}
