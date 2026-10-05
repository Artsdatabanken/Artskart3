using System.ComponentModel.DataAnnotations;
using Artskart3.Core.Application.Validation;

namespace Artskart3.Core.Application.DTOs;

public class CreateSavedFilterRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Navn er påkrevd.")]
    [MaxLength(200, ErrorMessage = "Navnet kan ikke være lengre enn 200 tegn.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Filter er påkrevd.")]
    public ObservationSearchFilterDto Filter { get; set; } = null!;

    public MapExtentDto? Extent { get; set; }

    public bool IsDefault { get; set; }

    // Samme grenser som søket, ellers kan det lagres filtre som aldri lar seg kjøre.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Filter != null && ObservationFilterValidator.GetError(Filter) is { } error)
        {
            yield return new ValidationResult(error, [nameof(Filter)]);
        }
    }
}
