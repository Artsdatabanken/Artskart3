using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Leverer områdene med geometri til geografitjenesten. Egen abstraksjon fordi
/// tjenesten laster dem én gang og holder dem i minnet — kilden er irrelevant for
/// oppslagslogikken, og en test skal kunne gi tre polygoner i stedet for 4 224.
/// </summary>
public interface IAreaGeometrySource
{
    /// <summary>
    /// Alle områder med geometri, gjeldende og historiske. Kalles én gang per
    /// tjenesteinstans.
    /// </summary>
    IReadOnlyList<AreaGeometry> LoadAll();
}
