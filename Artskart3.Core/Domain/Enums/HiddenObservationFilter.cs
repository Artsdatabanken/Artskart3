using System.Text.Json.Serialization;

namespace Artskart3.Core.Domain.Enums;

/// <summary>
/// Hva et søk gjør med funn skjult av annotering. Exclude er standardverdien, slik at
/// lagrede filtre og klienter som ikke kjenner feltet aldri får dem ved et uhell.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HiddenObservationFilter>))]
public enum HiddenObservationFilter
{
    Exclude = 0,
    Include = 1,
    Only = 2,
}

public static class HiddenObservationFilterExtensions
{
    /// <summary>Verdien HiddenByAnnotation må ha, eller null når alle funn er med.</summary>
    public static bool? RequiredHiddenValue(this HiddenObservationFilter filter) => filter switch
    {
        HiddenObservationFilter.Include => null,
        HiddenObservationFilter.Only => true,
        _ => false,
    };
}
