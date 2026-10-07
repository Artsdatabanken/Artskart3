namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Antall distinkte lokasjoner som matcher et søkefilter, cappet til
/// <see cref="Artskart3.Core.Constants.SearchConstants.MaxLocationResults"/>.
/// Truncated er true når det faktiske antallet overstiger grensen.
/// </summary>
public class LocationCountDto
{
    public required int Count { get; init; }
    public required bool Truncated { get; init; }
}
