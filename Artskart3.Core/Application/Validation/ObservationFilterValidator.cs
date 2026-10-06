using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Constants;

namespace Artskart3.Core.Application.Validation;

/// <summary>
/// Reglene et filter må oppfylle for å kunne kjøres. Brukes både av søket og når
/// filtre lagres, slik at et lagret filter alltid kan aktiveres igjen.
/// </summary>
public static class ObservationFilterValidator
{
    /// <summary>
    /// Feilmelding hvis filteret er ugyldig, ellers null.
    /// </summary>
    public static string? GetError(IObservationFilter filter)
    {
        if (filter.CoordinatePrecision is { From: > 0 and var from, To: > 0 and var to } && from > to)
        {
            return SearchConstants.CoordinatePrecisionInvalidMessage;
        }

        return GetArraySizeError(filter);
    }

    private static string? GetArraySizeError(IObservationFilter filter)
    {
        var max = SearchConstants.MaxFilterArraySize;

        ReadOnlySpan<(string name, int? length)> arrays =
        [
            (nameof(filter.TaxonGroupIds), filter.TaxonGroupIds?.Length),
            (nameof(filter.TaxonIds), filter.TaxonIds?.Length),
            (nameof(filter.CategoryIds), filter.CategoryIds?.Length),
            (nameof(filter.OrganizationIds), filter.OrganizationIds?.Length),
            (nameof(filter.MunicipalityIds), filter.MunicipalityIds?.Length),
            (nameof(filter.CountyIds), filter.CountyIds?.Length),
            (nameof(filter.RestrictedAreaIds), filter.RestrictedAreaIds?.Length),
            (nameof(filter.OceanAreaIds), filter.OceanAreaIds?.Length),
            (nameof(filter.BehaviorIds), filter.BehaviorIds?.Length),
            (nameof(filter.BasisOfRecordIds), filter.BasisOfRecordIds?.Length),
        ];

        foreach (var (name, length) in arrays)
        {
            if (length > max)
            {
                return $"{name} can contain at most {max} items.";
            }
        }

        // ObservationIds har egen grense — se SearchConstants.MaxObservationIdFilterSize.
        // Listen havner i en Contains mot 192M rader, på et anonymt endepunkt.
        if (filter.ObservationIds?.Length > SearchConstants.MaxObservationIdFilterSize)
        {
            return $"{nameof(filter.ObservationIds)} can contain at most {SearchConstants.MaxObservationIdFilterSize} items.";
        }

        return null;
    }
}
