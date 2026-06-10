namespace Artskart3.Core.Application.DTOs;

public class ExportColumnDefinition
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public bool IsDefaultSelected { get; set; }
    public string? Description { get; set; }
}
