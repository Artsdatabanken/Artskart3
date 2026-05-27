namespace Artskart3.Core.Application.DTOs
{
    public class CategoryTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public IEnumerable<CategoryDto> Categories { get; set; } = [];
    }
}
