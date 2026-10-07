namespace Artskart3.Core.Application.DTOs;

public class SavedFilterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public ObservationSearchFilterDto Filter { get; set; } = null!;
    public MapExtentDto? Extent { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}
