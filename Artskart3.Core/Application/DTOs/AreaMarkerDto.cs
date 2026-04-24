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
        public DateTime SyncDateTime { get; set; }
        public int? ObservationCount { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool IsCurrent { get; set; }
        public string? Centroid { get; set; }
    }
}
