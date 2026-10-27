namespace Artskart3.Core.Application.Import;

/// <summary>
/// Organisasjonsoppslagene importen trenger for å plassere en post hos en utgiver, et
/// datasett og eventuelle prosjekter.
///
/// NAVNENE ER ARTSKART 3s, OG DE BETYR NOE ANNET ENN I ARTSKART 2. Det er den eneste
/// virkelige fellen i dette registeret, så den står her:
///
/// | <c>OrganizationTypeId</c> | Artskart 3 | Artskart 2 | Kildefelt i DwC |
/// |---|---|---|---|
/// | 1 | Publisher — utgiver | <c>Institution</c> | <c>institutionCode</c> |
/// | 2 | Dataset | <c>Collection</c> — samling | <c>collectionCode</c> |
/// | 3 | Project — prosjekt | <c>Dataset</c> — datasett | <c>datasetID</c>, <c>datasetName</c> |
///
/// Et oppslag som heter <c>Dataset</c> her gjelder altså det Artskart 2 kalte en
/// samling, og det DwC kaller <c>datasetID</c> finner et <c>Project</c>. Kolonnen
/// <c>Observation.DatasetOrgId</c> peker på type 2; prosjektene ligger i
/// <c>ObservationProject</c>.
///
/// I ARTSKART 2 ER DETTE TO TJENESTER: <c>ICoreArtskartService</c> mot RavenDB og
/// <c>IOrganizationService</c> mot SQL-tabellen <c>Organization</c>, som er en speiling
/// av den første. Her er det én, fordi <c>Organization</c> er primærkilden.
///
/// OPPSLAGENE ER LESENDE. Å opprette et datasett eller et prosjekt fra en ukjent kode
/// er <see cref="IOrganizationCreator"/> sitt ansvar, og det må kunne slås av.
/// </summary>
public interface IOrganizationRegistry
{
    /// <summary>
    /// Utgiveren som har dette aliaset — en historisk institusjonskode som fortsatt
    /// står i kildedataene.
    ///
    /// DETTE OPPSLAGET GÅR FØRST, før <see cref="PublisherByCode"/>. Treffer ingen av
    /// dem, avvises posten. Mangler et alias, forsvinner postene fra den koden stille.
    /// </summary>
    OrganizationRef? PublisherByAlias(string alias);

    /// <summary>Utgiveren med denne koden (DwC <c>institutionCode</c>).</summary>
    OrganizationRef? PublisherByCode(string code);

    /// <summary>
    /// Datasettet med denne koden under utgiveren (DwC <c>collectionCode</c>, type 2).
    /// </summary>
    OrganizationRef? DatasetByCode(int publisherOrgId, string code);

    /// <summary>
    /// Prosjektet med denne DwC-<c>datasetID</c>-en under datasettet (type 3).
    ///
    /// DETTE OPPSLAGET GÅR FØRST, før <see cref="ProjectByName"/>, fordi navnet ikke er
    /// entydig: i dagens data finnes 902 par prosjekter som deler navn under samme
    /// datasett.
    /// </summary>
    OrganizationRef? ProjectBySourceId(int datasetOrgId, string sourceDatasetId);

    /// <summary>
    /// Prosjektet med dette navnet under datasettet. Deler flere prosjekter navnet,
    /// velges det eldste — se <see cref="ProjectBySourceId"/>.
    /// </summary>
    OrganizationRef? ProjectByName(int datasetOrgId, string name);
}

/// <summary>
/// En organisasjonsrad, slik importen trenger den.
/// </summary>
/// <param name="Id">
/// Blir <c>Observation.InstitutionOrgId</c> (type 1), <c>Observation.DatasetOrgId</c>
/// (type 2) eller <c>ObservationProject.ProjectOrgId</c> (type 3).
/// </param>
/// <param name="Code">Utgiver- eller datasettkoden. Prosjekter har ingen.</param>
/// <param name="Name">Navnet. For prosjekter er det <c>datasetName</c> fra kilden.</param>
/// <param name="ParentId">Utgiveren for et datasett, datasettet for et prosjekt.</param>
public sealed record OrganizationRef(int Id, string? Code, string Name, int? ParentId);
