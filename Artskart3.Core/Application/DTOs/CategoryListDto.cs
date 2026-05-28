namespace Artskart3.Core.Application.DTOs
{
    public class CategoryListDto
    {
        public string Name { get; set; } = "Categories";
        public CategoryTypeDto[] CategoryTypes { get; set; } = [];
    }
}
