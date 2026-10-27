namespace Artskart3.Core.Application.Taxonomy;

/// <summary>
/// Leser taksonomien fra Artskart 2s dokumentlager.
///
/// GRENSESNITTET FINNES FOR Å HOLDE RAVENDB UTE AV RESTEN AV LØSNINGEN. Implementasjonen
/// ligger i <c>Artskart3.Workers/Taxonomy</c> og er det eneste stedet som vet at kilden
/// er RavenDB. Når harvest-delen flytter dokumentene til SQL Server, byttes
/// implementasjonen og alt annet står urørt.
///
/// INGEN SKRIVING. Speilingen går én vei: dokumentlageret er master, SQL-kopien er
/// avledet. Det er samme forhold som i Artskart 2, bare med kopisteget gjort eksplisitt.
/// </summary>
public interface ITaxonomySource
{
    /// <summary>
    /// Alle taksoner, strømmet. Det er 180 000 av dem med navn, populærnavn og
    /// merkinger inni, så de skal ikke materialiseres i én liste.
    /// </summary>
    IAsyncEnumerable<TaxonInfoDocument> ReadTaxaAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Artsgruppene. Det er under hundre av dem, så en liste er greit.
    /// </summary>
    Task<IReadOnlyList<SpeciesGroupDocument>> ReadSpeciesGroupsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Hvor dataene kom fra, til bokføring. Skrives sammen med speilingen slik at
    /// paritetsriggen kan oppgi hvilken årgang den målte mot — det var nettopp det
    /// ingen kunne se da Artskart 3s taksonomi viste seg å være fem måneder eldre enn
    /// Artskart 2s.
    /// </summary>
    string Describe();
}
