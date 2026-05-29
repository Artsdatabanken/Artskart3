namespace Artskart3.Core.Application.DTOs
{
    public class AreaDto
    {
        public int Id { get; set; }
        public string Fid { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsCurrent { get; set; }
    }
}
