namespace Artskart3.Core.Application.DTOs
{
    public abstract class BaseDto
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
