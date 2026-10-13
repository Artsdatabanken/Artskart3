namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// En egenskap på taksonet, hentet fra <c>TaxonProperty</c>. Port av Artskart 2s
/// <c>TaxonPropertyInfo</c>.
///
/// <c>ImportTaxonomy</c> legger dem i <see cref="ImportContext.TaxonProperties"/>, og
/// <c>ImportStatusForRegion</c> bruker dem til å finne rødlistekategorien som gjelder
/// for regionen posten ligger i.
/// </summary>
/// <param name="Prefix">Hvilken liste egenskapen hører til, f.eks. rødliste eller fremmedartsliste.</param>
/// <param name="Context">Regionen eller sammenhengen egenskapen gjelder i.</param>
/// <param name="Property">Selve verdien, f.eks. kategorikoden.</param>
public sealed record TaxonPropertyInfo(string? Prefix, string? Context, string? Property);
