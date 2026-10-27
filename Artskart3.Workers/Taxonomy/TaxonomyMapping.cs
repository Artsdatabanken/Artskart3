using Artskart3.Core.Application.Taxonomy;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Oversetter Artskart 2s taksonomidokumenter til Artskart 3s entiteter.
///
/// EGEN KLASSE FORDI DET ER PARITETSKRAVET. Hver linje her svarer til en linje i
/// Artskart 2s <c>Taxon.MapFrom</c>, <c>TaxonName.MapFrom</c>,
/// <c>TaxonPopularName.MapFrom</c> og <c>TaxonProperty.MapFrom</c>. Avviker mappingen,
/// havner observasjoner på andre taksoner enn i Artskart 2 — og det er ikke noe
/// enhetstestene fanger hvis mappingen ligger gjemt inne i skrivingen.
/// </summary>
public static class TaxonomyMapping
{
    /// <summary>
    /// Taksonet, felt for felt fra Artskart 2s <c>Taxon.MapFrom</c>.
    ///
    /// <c>ParentTaxonId</c> FALLER TILBAKE PÅ HIERARKIET. Er den 0, tar Artskart 2
    /// første element i <c>TaxonIdHiarchy</c> som ikke er taksonet selv. Bevart.
    ///
    /// <c>ValidScientificName</c> FORETREKKER DEN FORMATERTE. Det er derfor navnene i
    /// indeksen bærer <c>&lt;i&gt;</c>-tagger, og riggen sammenligner dem ordrett.
    ///
    /// <c>ExternalTaxonId</c> RØRES IKKE. Feltet finnes ikke i Artskart 2, og
    /// <c>ObservationDetailsRepository</c> faller tilbake på <c>TaxonId</c> så lenge det
    /// er 0 — så 0 er den riktige verdien, ikke en manglende en.
    /// </summary>
    public static void Apply(Taxon rad, TaxonInfoDocument dokument, DateTime nå)
    {
        ArgumentNullException.ThrowIfNull(rad);
        ArgumentNullException.ThrowIfNull(dokument);

        rad.TaxonRankId = (int)dokument.CategoryValue;
        rad.ParentTaxonId = dokument.ParentTaxonId == 0
            ? dokument.TaxonIdHiarchy.FirstOrDefault(x => x != dokument.TaxonId)
            : dokument.ParentTaxonId;
        rad.ValidScientificNameId = dokument.ValidScientificNameId;
        rad.ValidScientificName = dokument.ValidScientificNameFormatted ?? dokument.ValidScientificName;
        rad.ValidScientificNameAuthorship = dokument.ValidScientificNameAuthorship;
        rad.PreferredPopularName = dokument.PrefferedPopularname;
        rad.TaxonGroupId = dokument.TaxonGroupId;
        rad.ExistsInCountry = dokument.ExistsInCountry;
        rad.ScientificNameIdHiarchy = string.Join(',', dokument.ScientificNameIdHiarchy);
        rad.TaxonIdHiarchy = string.Join(',', dokument.TaxonIdHiarchy);
        rad.IsDeleted = dokument.IsDeleted;
        rad.DeletedAt = dokument.IsDeleted ? rad.DeletedAt ?? nå : null;
        rad.UpdatedAt = nå;
    }

    /// <summary>
    /// Navnet. Også her foretrekkes den formaterte varianten, slik Artskart 2 gjør.
    /// </summary>
    public static void Apply(
        TaxonName rad, ScientificNameAlternativeDocument dokument, int taksonId, DateTime nå)
    {
        ArgumentNullException.ThrowIfNull(rad);
        ArgumentNullException.ThrowIfNull(dokument);

        rad.Accepted = dokument.Accepted;
        rad.ScientificName = dokument.ScientificNameFormatted ?? dokument.ScientificName ?? string.Empty;
        rad.ScientificNameAuthorship = dokument.ScientificNameAuthorship;
        rad.TaxonId = taksonId;
        rad.UpdatedAt = nå;
    }

    public static TaxonPopularName ToPopularName(PopularNameDocument dokument, int taksonId, DateTime nå)
    {
        ArgumentNullException.ThrowIfNull(dokument);

        return new TaxonPopularName
        {
            SourceId = dokument.vernacularNameID,
            Language = dokument.language ?? string.Empty,
            Name = dokument.Name ?? string.Empty,
            Preferred = dokument.Preffered,
            TaxonId = taksonId,
            CreatedAt = nå,
            UpdatedAt = nå
        };
    }

    public static TaxonProperty ToProperty(TaxonTagDocument dokument, int taksonId, DateTime nå)
    {
        ArgumentNullException.ThrowIfNull(dokument);

        return new TaxonProperty
        {
            TagGroup = dokument.TagGroup ?? string.Empty,
            Prefix = dokument.Prefix ?? string.Empty,
            Context = dokument.Context ?? string.Empty,
            Tag = dokument.Tag ?? string.Empty,
            Url = dokument.Url ?? string.Empty,
            ScientificName = dokument.ScientificName,
            TaxonId = taksonId,
            CreatedAt = nå,
            UpdatedAt = nå
        };
    }
}
