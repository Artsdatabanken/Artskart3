namespace Artskart3.Core.Application.Services;

/// <summary>
/// Luker bort områder som ikke kan nå kartutsnittet.
///
/// Ligger for seg selv, ikke inne i AreaHierarchyService, fordi reglene her er
/// det som avgjør om svaret blir riktig — og de kan ikke testes gjennom en
/// tjeneste som må laste 134 millioner indeksrader først.
/// </summary>
public static class AreaBoundsPruner
{
    /// <summary>
    /// Beholder de ID-ene som kan ha en observasjonslokasjon i utsnittet.
    ///
    /// TRE REGLER, ALLE MED SAMME BEGRUNNELSE: HELLER FOR MYE ENN FOR LITE
    ///
    /// 1. Samme ID kan gjelde flere områdetyper. Fylkes-ID-er slås opp mot både
    ///    fylke og Svalbard, og da holder det at ÉN av dem når fram.
    ///
    /// 2. En ID uten kjent boks beholdes. Den kan være et område uten
    ///    observasjoner — som uansett ikke gir treff — men den kan like gjerne
    ///    være nyimportert og ennå ikke med i oppslaget. Da skal databasen
    ///    avgjøre, ikke vi.
    ///
    /// 3. Er oppslaget tomt, gjøres ingen luking. Uten den regelen ville en
    ///    tjeneste som ikke har rukket å laste ennå tømt hvert eneste
    ///    områdefilter og svart tomt på ekte søk.
    /// </summary>
    public static int[] Prune(
        int[] entityIds,
        AreaBounds envelope,
        IReadOnlyDictionary<(int EntityTypeId, int EntityId), AreaBounds> bounds,
        params int[] entityTypeIds)
    {
        if (entityIds.Length == 0 || entityTypeIds.Length == 0 || bounds.Count == 0)
        {
            return entityIds;
        }

        return entityIds.Where(id =>
        {
            var kjent = false;

            foreach (var type in entityTypeIds)
            {
                if (!bounds.TryGetValue((type, id), out var boks)) continue;
                kjent = true;
                if (boks.Intersects(envelope)) return true;
            }

            return !kjent;
        }).ToArray();
    }
}
