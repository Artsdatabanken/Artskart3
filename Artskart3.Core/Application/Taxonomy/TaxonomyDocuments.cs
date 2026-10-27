using System.Text.Json.Serialization;

namespace Artskart3.Core.Application.Taxonomy;

/// <summary>
/// Et dokument som bærer kildens egen nøkkel. Nøkkelen står i metadataene, ikke i
/// kroppen, og flere av dokumentene har ingen id-felt i JSON i det hele tatt.
/// </summary>
public interface ISourceDocument
{
    string? DocumentKey { get; set; }
}

/// <summary>
/// Et takson slik det ligger i Artskart 2s dokumentlager, speilet felt for felt fra
/// <c>Artskart.Model.Public.TaxonInfo</c>.
///
/// DETTE ER IKKE ARTSNAVNEBASENS FORMAT. Det er Artskart 2s forenklede kopi, som ifølge
/// dokumentasjonen der synkroniseres nattlig fra Artsnavnebasen. Skal Artskart 3 en dag
/// lese Artsnavnebasen direkte, er det et annet format og en annen oppgave.
///
/// NAVNENE FØLGER KILDEN, inkludert skrivefeilene: <c>PrefferedPopularname</c>,
/// <c>ScientificNameIdHiarchy</c>, <c>TaxonIdHiarchy</c>. De er navnene i JSON-en, og
/// det er dem deserialiseringen må treffe. Artskart 3s egne entiteter har rettet
/// skrivemåten, og mappingen oversetter.
/// </summary>
public sealed class TaxonInfoDocument : ISourceDocument
{
    /// <summary>
    /// Taksonets id. STÅR IKKE I JSON-KROPPEN — den er dokumentnøkkelen
    /// (<c>TaxonInfos/47871</c>), og Artskart 2s klient fyller den derfra. Lesesiden
    /// gjør det samme.
    /// </summary>
    public int Id { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public string? DocumentKey { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    /// Rangen. Lagres som streng av RavenDBs standardoppsett, men kan være tall —
    /// derfor leses den gjennom en konverter som tar begge.
    /// </summary>
    public TaxonomicCategory CategoryValue { get; set; }

    public int TaxonId { get; set; }

    public int ParentTaxonId { get; set; }

    public int ValidScientificNameId { get; set; }

    public string? ValidScientificName { get; set; }

    public string? ValidScientificNameFormatted { get; set; }

    public string? ValidScientificNameAuthorship { get; set; }

    public string? PrefferedPopularname { get; set; }

    public int TaxonGroupId { get; set; }

    public bool ExistsInCountry { get; set; }

    /// <summary>
    /// Taksonet er slettet i kilden, men beholdes: observasjoner kan fortsatt peke på
    /// det. Artskart 2 sier det rett ut — «Taxon is kept for history - in case records
    /// still points towards it» — og det er grunnen til at speilingen aldri
    /// hardsletter.
    /// </summary>
    public bool IsDeleted { get; set; }

    public int[] ScientificNameIdHiarchy { get; set; } = [];

    public int[] TaxonIdHiarchy { get; set; } = [];

    public List<ScientificNameAlternativeDocument> ScientificNames { get; set; } = [];

    public List<PopularNameDocument> PopularNames { get; set; } = [];

    public List<TaxonTagDocument> TaxonTags { get; set; } = [];
}

/// <summary>
/// Et vitenskapelig navn på taksonet. <c>ScientificNameId</c> er Artsnavnebasens id og
/// blir <c>TaxonName.Id</c> i indeksen — den er ikke en autogenerert nøkkel.
/// </summary>
public sealed class ScientificNameAlternativeDocument
{
    public int ScientificNameId { get; set; }

    public bool Accepted { get; set; }

    public string? ScientificName { get; set; }

    /// <summary>
    /// Navnet med kursivmarkering. Artskart 2 foretrekker denne når den finnes, og det
    /// er derfor navnene i indeksen inneholder <c>&lt;i&gt;</c>-tagger.
    /// </summary>
    public string? ScientificNameFormatted { get; set; }

    public string? ScientificNameAuthorship { get; set; }
}

/// <summary>Et populærnavn. Feltnavnene er små i kilden og beholdes slik.</summary>
public sealed class PopularNameDocument
{
    public int vernacularNameID { get; set; }

    public string? language { get; set; }

    public string? Name { get; set; }

    public bool Preffered { get; set; }
}

/// <summary>
/// En kontekstmerking på taksonet — rødliste, fremmedartsliste og liknende.
/// </summary>
public sealed class TaxonTagDocument
{
    public string? TagGroup { get; set; }

    public string? Prefix { get; set; }

    public string? Context { get; set; }

    public string? Tag { get; set; }

    public string? Url { get; set; }

    public string? ScientificName { get; set; }
}

/// <summary>
/// En artsgruppe. <c>PresentationName</c> er navnet som vises; <c>SpeciesGroupId</c> er
/// kildens tekstlige nøkkel og brukes ikke av indeksen.
/// </summary>
public sealed class SpeciesGroupDocument : ISourceDocument
{
    /// <summary>Fra dokumentnøkkelen, som for taksonene.</summary>
    public int Id { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public string? DocumentKey { get; set; }

    /// <summary>
    /// Kildens egen talnøkkel. Artskart 2s <c>SpeciesGroup.RId</c> er en beregnet
    /// egenskap som returnerer <c>Id</c>, og RavenDB serialiserte den — så kroppen har
    /// <c>RId</c> der <c>Id</c> mangler. Brukes som reserve om nøkkelen ikke kan tolkes.
    /// </summary>
    public int RId { get; set; }

    public string? SpeciesGroupId { get; set; }

    public string? PresentationName { get; set; }
}
