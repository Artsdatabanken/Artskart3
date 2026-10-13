namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Taksonet med det importen trenger av det. Flat kopi av en <c>Taxon</c>-rad med
/// hierarkiet og egenskapene løst opp.
/// </summary>
/// <param name="Id">Blir <c>Observation.TaxonId</c>.</param>
/// <param name="RankId">Rangen. Lavere tall er høyere rang.</param>
/// <param name="ValidScientificName">
/// Taksonets gyldige navn. Importen overskriver <c>record.ScientificName</c> med dette
/// og lagrer kildens verdi som <c>_original_ScientificName</c>.
/// </param>
/// <param name="ValidScientificNameAuthorship">Samme for forfatterstrengen.</param>
/// <param name="TaxonGroupId">
/// Blir <c>Observation.TaxonGroupId</c>. Mangler den, importeres posten med varselet
/// <c>NoTaxonGroupHit</c> — det er ikke en avvisning.
/// </param>
/// <param name="TaxonIdHierarchy">
/// Taksonet selv først, så forfedrene opp til roten. Blir én rad per nivå i
/// <c>ObservationTaxons</c>, og er det taksonfilteret utvider seg over.
/// </param>
/// <param name="ScientificNameIds">
/// Alle navne-ID-ene taksonet har. Annotasjonene matcher på disse (fase 10).
/// </param>
/// <param name="Properties">
/// Taksonets egenskaper — rødlistekategori og lignende. <c>ImportStatusForRegion</c>
/// leser dem fra konteksten.
/// </param>
public sealed record TaxonRecord(
    int Id,
    int RankId,
    string? ValidScientificName,
    string? ValidScientificNameAuthorship,
    int TaxonGroupId,
    int[] TaxonIdHierarchy,
    int[] ScientificNameIds,
    IReadOnlyList<TaxonPropertyInfo> Properties);

/// <summary>
/// Et treff i navneoppslaget: taksonet, og hvilket av navnene dets som traff.
/// </summary>
/// <param name="Taxon">Taksonet.</param>
/// <param name="MatchedScientificNameId">
/// Navne-ID-en som traff. Blir <c>Observation.MatchedScientificNameId</c>, og er det
/// som skiller «funnet som synonym» fra «funnet som gyldig navn».
/// </param>
/// <param name="MatchedScientificName">Navnet som traff, med formatering.</param>
/// <param name="Diagnostics">
/// Tom ved rene treff. Ved flertydighet står kandidatene her, og teksten havner i
/// avvisningens <c>ErrorContext</c> — det er ofte det eneste sporet av hvorfor et navn
/// ikke kunne bestemmes.
/// </param>
public sealed record TaxonMatch(
    TaxonRecord Taxon,
    int MatchedScientificNameId,
    string? MatchedScientificName,
    string Diagnostics);

/// <summary>
/// Hele taksonomien slik navneoppslaget laster den: 152 756 taksoner, 228 639 navn,
/// 84 176 egenskaper (målt i <c>Artskart3IndexProdLikeTestMigrations</c>).
/// </summary>
public sealed record TaxonomySnapshot(
    IReadOnlyList<TaxonRecord> Taxa,
    IReadOnlyList<TaxonNameEntry> Names);
