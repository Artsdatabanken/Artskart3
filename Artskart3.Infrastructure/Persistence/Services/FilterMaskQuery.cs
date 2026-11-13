using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Infrastructure.Persistence.Services;

public static class FilterMaskQuery
{
    /// <summary>
    /// Maskepredikatet. Er ingen bool satt, legges ingenting til, og cellens masker
    /// summeres av grupperingen som følger.
    ///
    /// Skjulte funn er et intervall, ikke en bitoperasjon: biten er den øverste, så
    /// «under 32» betyr at den er av. Standardfilteret står på nesten hvert oppslag, og
    /// SQL Server kan ikke estimere <c>Mask &amp; @care = @want</c>: samme spørring fikk
    /// planer for både 1 og 100 000 rader.
    /// </summary>
    public static IQueryable<T> WhereMask<T>(this IQueryable<T> query, FilterMask.Selection maske)
        where T : class, IHarFilterMaske
    {
        if (!maske.IsActive) return query;

        if ((maske.Care & FilterMask.HiddenByAnnotation) != 0)
        {
            query = (maske.Want & FilterMask.HiddenByAnnotation) != 0
                ? query.Where(r => r.Mask >= FilterMask.HiddenByAnnotation)
                : query.Where(r => r.Mask < FilterMask.HiddenByAnnotation);
        }

        var care = (byte)(maske.Care & ~FilterMask.HiddenByAnnotation);
        if (care == 0) return query;

        var want = (byte)(maske.Want & ~FilterMask.HiddenByAnnotation);
        return query.Where(r => (r.Mask & care) == want);
    }
}
