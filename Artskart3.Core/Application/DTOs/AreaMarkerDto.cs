namespace Artskart3.Core.Application.DTOs
{
    public class AreaMarkerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int AreaTypeId { get; set; }
        public int? ObservationCount { get; set; }
        public string? Centroid { get; set; } // Er det bedre å sende å sende koordinater, så slipper frontend å bruke energi på å parse?
    }
}
