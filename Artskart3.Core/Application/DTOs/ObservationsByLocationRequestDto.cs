namespace Artskart3.Core.Application.DTOs;

public class ObservationsByLocationRequestDto
{
    public IEnumerable<int> Ids { get; set; } = [];
    public ObservationSearchFilterDto? Filter { get; set; }
}
