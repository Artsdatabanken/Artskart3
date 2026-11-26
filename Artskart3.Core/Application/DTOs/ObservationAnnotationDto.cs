namespace Artskart3.Core.Application.DTOs;

public class ObservationAnnotationDto
{
    public string? Author { get; init; }
    public DateTime? Created { get; init; }
    public string? Comment { get; init; }
    public List<ObservationAnnotationChangeDto> Changes { get; init; } = [];
}

/// <summary>Field er DwC-navnet fra annoteringen, for eksempel Longitude.</summary>
public record ObservationAnnotationChangeDto(string Field, string? OriginalValue, string? NewValue);
