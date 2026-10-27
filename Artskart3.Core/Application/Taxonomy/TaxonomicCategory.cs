namespace Artskart3.Core.Application.Taxonomy;

/// <summary>
/// Taksonomisk rang, slik Artsnavnebasen nummererer den.
///
/// VERDIENE ER BÆRENDE. De skrives til <c>Taxon.TaxonRankId</c> og brukes som
/// <c>TaxonRank.Id</c>, så de kan ikke omnummereres. Artskart 2 fyller
/// <c>TaxonRank</c>-tabellen fra sin egen <c>Category</c>-enum — ikke fra RavenDB — og
/// dette er den samme listen med de samme tallene.
///
/// Målt i gullsettene: rang 4 er <c>subphylum</c> og rang 6 er <c>class</c> i begge
/// basene, og det var nettopp den forskjellen som flyttet «Anthozoa» mellom
/// taksonomisnapshotene.
///
/// <c>NotSet</c> har med vilje ingen eksplisitt verdi: Artskart 2 lar den få neste
/// ledige tall fra enumen, og den oppførselen er bevart.
/// </summary>
public enum TaxonomicCategory
{
    Kingdom = 1,
    Subkingdom = 2,
    Phylum = 3,
    Subphylum = 4,
    Superclass = 5,
    Class = 6,
    Subclass = 7,
    Infraclass = 8,
    Cohort = 9,
    Superorder = 10,
    Order = 11,
    Suborder = 12,
    Infraorder = 13,
    Superfamily = 14,
    Family = 15,
    Subfamily = 16,
    Tribe = 17,
    Subtribe = 18,
    Genus = 19,
    Subgenus = 20,
    Section = 21,
    Species = 22,
    Subspecies = 23,
    Variety = 24,
    Form = 25,
    NotSet
}
