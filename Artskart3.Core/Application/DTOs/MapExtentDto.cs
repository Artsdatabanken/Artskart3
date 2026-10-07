using System.ComponentModel.DataAnnotations;

namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Kartutsnitt i EPSG:25833.
/// </summary>
public class MapExtentDto : IValidatableObject
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!double.IsFinite(MinX) || !double.IsFinite(MinY) || !double.IsFinite(MaxX) || !double.IsFinite(MaxY))
        {
            yield return new ValidationResult("Kartutsnittet må bestå av endelige tall.");
        }
        else if (MinX >= MaxX || MinY >= MaxY)
        {
            yield return new ValidationResult("Kartutsnittet må ha Min mindre enn Max.");
        }
    }
}
