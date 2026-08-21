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
}
