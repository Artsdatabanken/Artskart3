namespace Artskart3.Core.Application.Import;

/// <summary>
/// Hvilke poster en cache-lesing skal ta med. Cachen sletter aldri fysisk; en slettet
/// post står igjen med <c>IsDeleted = 1</c>.
///
/// Port av Artskart 2s <c>EntityStatus</c>. Importen der velger verdi etter modus:
/// <c>ReprocessDeleted</c> gir <see cref="OnlyDeleted"/>, <c>Reprocess</c> og
/// <c>ReprocessNoHash</c> gir <see cref="NotDeleted"/>, og normal inkrementell import
/// gir <see cref="All"/> — den må se slettingene for å kunne speile dem.
/// </summary>
public enum CacheEntityStatus
{
    All = 0,
    NotDeleted = 1,
    OnlyDeleted = 2
}
