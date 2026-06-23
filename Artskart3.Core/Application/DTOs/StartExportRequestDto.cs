namespace Artskart3.Core.Application.DTOs;

public class StartExportRequestDto
{
    public ObservationSearchFilterDto Filter { get; set; } = null!;
    public List<string> SelectedColumns { get; set; } = [];
}
