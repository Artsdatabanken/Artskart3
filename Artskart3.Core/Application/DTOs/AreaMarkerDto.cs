namespace Artskart3.Core.Application.DTOs
{
    public class AreaMarkerDto
    {
        public int Id { get; set; }
        public string DocumentId { get; set; } = null!;
        public string Fid { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int AreaTypeId { get; set; }
        public string ParentFid { get; set; } = null!;
        public int? ObservationCount { get; set; }
        public string? WktsPolygon { get; set; }
    }
}
