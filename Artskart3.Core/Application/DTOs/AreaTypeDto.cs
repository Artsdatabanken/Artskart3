namespace Artskart3.Core.Application.DTOs
{
    public class AreaTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public IEnumerable<AreaDto> Areas { get; set; } = [];
    }
}
