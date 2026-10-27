using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Datakilderegisteret — én rad per node importen kan kjøre. Speiling av Artskart 2s
/// <c>SourceDataBase</c>, som ligger i RavenDB.
///
/// FELTENE ER SPEILET FELT FOR FELT, harvest-feltene inkludert, selv om harvest kommer
/// senere. En tabell som fra starten har riktig form slipper en ny migrasjon over
/// samme data.
///
/// <c>ImportState</c> er den samme noden sett fra importens side, 1:1 på <c>Id</c>:
/// denne tabellen er konfigurasjon, den andre er løpende tilstand.
///
/// GJENNOM OVERGANGEN EIER ARTSKART 2 RADENE. Både admin-flaten og harvest skriver til
/// RavenDB, og en speiling holder denne kopien fersk. Speilingen må være feltvis, ikke
/// en full overskriving: fra det punktet Artskart 3 overtar importen skriver den selv
/// organisasjoner og varianter, mens Artskart 2 fortsatt oppdaterer
/// <see cref="Name"/>, <see cref="Notes"/>, <see cref="GbifDataSetId"/>,
/// <see cref="GbifDoi"/> og <see cref="GbifPublisherId"/>.
/// </summary>
public class DataSource
{
    /// <summary>
    /// Nodenummeret. Settes av kilden, ikke av databasen — det er den samme Id-en som
    /// <c>ImportState.Id</c>, <c>Observation.NodeId</c> og nodenummeret i cachen.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nodens navn. Vedlikeholdes manuelt i admin, men overskrives av harvest for
    /// GBIF IPT-kilder som ingen har redigert — se <see cref="IsEditedNameOrNotes"/>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Hvilken protokoll harvest skal snakke med kilden over.</summary>
    public RemoteDataSourceType RemoteDataSourceType { get; set; }

    /// <summary>Antall poster harvest henter per kall.</summary>
    public int RecordsBatchSize { get; set; }

    /// <summary>Versjon av webtjenesten kilden tilbyr.</summary>
    public int RemoteDataSourceV { get; set; }

    /// <summary>Adressen harvest henter fra.</summary>
    public string? RemoteDataSourceAdress { get; set; }

    /// <summary>Navnet på arkivet hos en GBIF IPT-kilde.</summary>
    public string? GbifIptArchiveName { get; set; }

    /// <summary>Interne merknader. Samme redigeringsregel som <see cref="Name"/>.</summary>
    public string? Notes { get; set; }

    /// <summary>Satt når kilden skal slettes.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Satt når navn eller merknader er redigert manuelt. Da skal harvest ikke
    /// overskrive dem med kildens egne metadata.
    /// </summary>
    public bool IsEditedNameOrNotes { get; set; }

    /// <summary>
    /// Satt for kilder der <c>occurrenceID</c> ikke er til å stole på. Harvest tømmer
    /// feltet på hver post, slik at identiteten utledes av katalognummeret i stedet.
    /// </summary>
    public bool NonValidOccuranceIds { get; set; }

    /// <summary>Spørringen harvest sender til GBIFs API.</summary>
    public string? GbifApiQuery { get; set; }

    /// <summary>
    /// Feltoverstyringer harvest anvender på hver post. JSON på formen
    /// <c>{"Feltnavn":"verdi"}</c>.
    /// </summary>
    public string? HarvestPropertyOverrides { get; set; }

    /// <summary>
    /// Feltoverstyringer importen anvender på hver post, rett etter lesing fra cache og
    /// før stegkjeden. Samme JSON-form som
    /// <see cref="HarvestPropertyOverrides"/>.
    /// </summary>
    public string? ImportPropertyOverrides { get; set; }

    /// <summary>Hvor ofte harvest skal hente fra kilden.</summary>
    public int UpdateFrequencyInDays { get; set; }

    /// <summary>
    /// Datasettets Id hos GBIF. VEDLIKEHOLDES AV HARVEST, og importen er avhengig av
    /// den: annotasjoner hentes per GBIF-datasett. Et frosset uttrekk av registeret
    /// begynner å drive fra første harvest-kjøring, og symptomet er at annotasjoner
    /// hentes for feil eller tom datasett-Id — stille.
    /// </summary>
    public Guid GbifDataSetId { get; set; }

    /// <summary>Datasettets DOI hos GBIF.</summary>
    public string? GbifDoi { get; set; }

    /// <summary>Utgiverens Id hos GBIF.</summary>
    public Guid GbifPublisherId { get; set; }

    /// <summary>
    /// Satt når hele datasettet er sensitivt. Da maskeres alle poster fra noden,
    /// uavhengig av art.
    /// </summary>
    public bool IsSensitive { get; set; }
}
