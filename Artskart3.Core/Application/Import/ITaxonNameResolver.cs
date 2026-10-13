using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Slår et artsnavn opp i taksonomien. Port av Artskart 2s <c>ITaxonSearchService</c>.
/// </summary>
public interface ITaxonNameResolver
{
    /// <summary>
    /// Finner taksonet et navn hører til, eller <c>null</c>. Treffer det ikke, avvises
    /// posten med <c>NoTaxonomicMatch</c>.
    /// </summary>
    /// <param name="name">
    /// Navnet, normalisert av <c>TaxonomyHelper.CleanScientificName</c> — altså i små
    /// bokstaver og uten rangforkortelser. Oppslaget forutsetter det.
    /// </param>
    /// <param name="recordTaxonRank">
    /// Rangen kilden oppgir, eller den importen utleder. Brukes bare til å skille
    /// flertydige treff.
    /// </param>
    /// <param name="authorString">Forfatterstrengen, i små bokstaver.</param>
    TaxonMatch? Resolve(string name, string? recordTaxonRank, string? authorString);
}

/// <summary>
/// Leverer taksonomien til navneoppslaget. Egen abstraksjon av samme grunn som for
/// områdene: oppslagslogikken skal kunne testes med fem navn i stedet for 228 639.
/// </summary>
public interface ITaxonomySource
{
    TaxonomySnapshot LoadAll();
}
