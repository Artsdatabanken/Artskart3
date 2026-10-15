using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
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

        // Gyldige verdier er 1-3, se Observation.RegistrationStatusId. Verdier utenfor
        // området kunne ikke treffe noe likevel, men de smalnes til byte i spørringene,
        // og C# caster unchecked: (byte)256 er 0. Avvises de her, kan ingen sti lenger
        // bli stående med 0 og måtte gjette om det betyr «funnet» eller «ikke satt».
        if (filter.RegistrationStatusId is < 1 or > 3)
        {
            return $"{nameof(filter.RegistrationStatusId)} must be between 1 and 3.";
        }

        if (GetPeriodError(filter) is { } periodError)
        {
            return periodError;
        }

        return GetArraySizeError(filter);
    }

    /// <summary>
    /// Årstallene må ligge innenfor det en DateTime kan uttrykke, og måneder må være
    /// måneder.
    ///
    /// Årstallene er ikke kosmetikk: filterstiene bygger <c>new DateTime(år, …)</c> av
    /// dem, og år 0 kastet en ArgumentOutOfRangeException som ble pakket til en
    /// ApplicationException og kom ut som HTTP 500 på et anonymt endepunkt.
    /// <see cref="PeriodBounds"/> klemmer nå verdiene, men en ugyldig forespørsel skal
    /// avvises som ugyldig, ikke stille bli et annet søk.
    /// </summary>
    private static string? GetPeriodError(IObservationFilter filter)
    {
        if (filter.Period is not { } period)
        {
            return null;
        }

        if (period.From is { } fra && fra is < PeriodBounds.MinYear or > PeriodBounds.MaxYear)
        {
            return $"Period.From must be between {PeriodBounds.MinYear} and {PeriodBounds.MaxYear}.";
        }

        if (period.To is { } til && til is < PeriodBounds.MinYear or > PeriodBounds.MaxYear)
        {
            return $"Period.To must be between {PeriodBounds.MinYear} and {PeriodBounds.MaxYear}.";
        }

        if (period is { From: { } f, To: { } t } && f > t)
        {
            return "Period.From cannot be later than Period.To.";
        }

        // Månedslista har ingen egen grense i GetArraySizeError, og den havner i en
        // Contains mot indekstabellen. Tolv distinkte verdier finnes; alt over det er
        // duplikater eller søppel, og 100 000 av dem skal ikke sendes til databasen.
        if (period.Months?.Length > 12)
        {
            return "Period.Months can contain at most 12 items.";
        }

        if (period.Months?.Any(m => m is < 1 or > 12) == true)
        {
            return "Period.Months must contain values between 1 and 12.";
        }

        return null;
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
            (nameof(filter.DatasetOrgIds), filter.DatasetOrgIds?.Length),
            (nameof(filter.ProjectOrgIds), filter.ProjectOrgIds?.Length),
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
