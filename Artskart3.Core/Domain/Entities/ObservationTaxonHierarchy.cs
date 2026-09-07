namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Denormalisert hierarkitabell for raske taksonomiske oppslag.
/// Hver rad knytter en observasjon til alle sine forfedre i taksonomien,
/// med én kolonne per rangsnivå (kingdom, phylum, class, osv.).
/// </summary>
public class ObservationTaxonHierarchy
{
    public int ObservationId { get; set; }

    public int? KingdomTaxonId { get; set; }       // TaxonRankId 1
    public int? SubkingdomTaxonId { get; set; }    // TaxonRankId 2
    public int? PhylumTaxonId { get; set; }        // TaxonRankId 3
    public int? SubphylumTaxonId { get; set; }     // TaxonRankId 4
    public int? SuperclassTaxonId { get; set; }    // TaxonRankId 5
    public int? ClassTaxonId { get; set; }         // TaxonRankId 6
    public int? SubclassTaxonId { get; set; }      // TaxonRankId 7
    public int? InfraclassTaxonId { get; set; }    // TaxonRankId 8
    public int? CohortTaxonId { get; set; }        // TaxonRankId 9
    public int? SuperorderTaxonId { get; set; }    // TaxonRankId 10
    public int? OrderTaxonId { get; set; }         // TaxonRankId 11
    public int? SuborderTaxonId { get; set; }      // TaxonRankId 12
    public int? InfraorderTaxonId { get; set; }    // TaxonRankId 13
    public int? SuperfamilyTaxonId { get; set; }   // TaxonRankId 14
    public int? FamilyTaxonId { get; set; }        // TaxonRankId 15
    public int? SubfamilyTaxonId { get; set; }     // TaxonRankId 16
    public int? TribeTaxonId { get; set; }         // TaxonRankId 17
    public int? SubtribeTaxonId { get; set; }      // TaxonRankId 18
    public int? GenusTaxonId { get; set; }         // TaxonRankId 19
    public int? SubgenusTaxonId { get; set; }      // TaxonRankId 20
    public int? SectionTaxonId { get; set; }       // TaxonRankId 21
    public int? SpeciesTaxonId { get; set; }       // TaxonRankId 22
    public int? SubspeciesTaxonId { get; set; }    // TaxonRankId 23
    public int? VarietyTaxonId { get; set; }       // TaxonRankId 24
    public int? FormTaxonId { get; set; }          // TaxonRankId 25
    public int? NotSetTaxonId { get; set; }        // TaxonRankId 26
}
