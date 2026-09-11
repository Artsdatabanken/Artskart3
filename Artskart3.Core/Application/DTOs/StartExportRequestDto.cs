using System.ComponentModel.DataAnnotations;

namespace Artskart3.Core.Application.DTOs;

public class StartExportRequestDto
{
    [MaxLength(200, ErrorMessage = "Navnet kan ikke være lengre enn 200 tegn.")]
    public string? Name { get; set; }

    public ObservationSearchFilterDto Filter { get; set; } = null!;
    public List<string> SelectedColumns { get; set; } = [];
}
