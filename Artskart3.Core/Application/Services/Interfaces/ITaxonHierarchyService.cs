using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Oppstartslastet oppslag for taksonhierarki.
/// Data lastes fra Taxon-tabellen og holdes i minne, oppdateres periodisk.
/// </summary>
public interface ITaxonHierarchyService
{
    /// <summary>
    /// Returnerer TaxonRankId for et gitt taxonId, eller null hvis ukjent.
    /// Brukes for å bestemme hvilken kolonne i ObservationTaxonHierarchy som skal spørres.
    /// </summary>
    int? GetTaxonRankId(int taxonId);

    /// <summary>
    /// Returnerer direkte barn av en gitt taxon som trenoder.
    /// Hvis parentTaxonId er null, returneres rotnodene (kingdom-nivå).
    /// Filtrerer til kun taxoner med observasjoner eller som finnes i landet.
    /// </summary>
    List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId);

    /// <summary>
    /// Returnerer foreldrekjeden (id-er fra rotnivå til nærmeste forelder) for hvert oppgitt taxonId.
    /// Ukjente taxonId-er får en tom kjede. Filtreres ikke på observasjonsantall.
    /// </summary>
    List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds);

    /// <summary>
    /// Returnerer alle etterkommere på art-nivå (rang 22) for et gitt taxonId.
    /// Brukes for å konvertere høyere-rangs filter til SpeciesTaxonId-oppslag.
    /// </summary>
    List<int> GetDescendantSpeciesIds(int taxonId);

    /// <summary>
    /// Returnerer alle etterkommere på et gitt rangnivå for et taxonId.
    /// Brukes for å konvertere f.eks. klasse til underliggende ordener.
    /// </summary>
    List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId);
}
