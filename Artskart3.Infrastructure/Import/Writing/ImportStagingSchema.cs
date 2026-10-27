using Artskart3.Core.Application.Import.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// Hele skrivebildet: hvilke tolv tabeller importen fyller, hvilke kolonner som settes,
/// og hvor verdien kommer fra. Dette er svaret på «hva skriver importen, og hvor».
///
/// BARNERADENE BÆRER <c>ProxyId</c>, IKKE <c>ObservationId</c>. <c>Observation.Id</c> er
/// en identitetskolonne, så den finnes ikke før raden er publisert. Alternativet er å
/// fange nye Id-er med <c>MERGE ... OUTPUT</c> og så koble barna på dem; å bære
/// forretningsnøkkelen hele veien er enklere, og <c>IX_Observation_ProxyId</c> finnes
/// allerede. Målt 14. oktober 2026: <c>ProxyId</c> er unik over alle 61 052 216
/// observasjoner, så koblingen er entydig.
///
/// LOKALITETSRADENE BÆRER <c>LookupId</c> av samme grunn.
///
/// GEOMETRI STAGES SOM WKB, ikke som en <c>geometry</c>-kolonne. Da trengs ingen
/// <c>Microsoft.SqlServer.Types</c>-avhengighet i bulk-kopieringen, og publiseringen
/// bygger geometrien med <c>geometry::STGeomFromWKB(@wkb, 25833)</c>. SRID-en settes
/// der, på ett sted — se migrasjonen <c>RelabelGeometrySridTo25833</c>.
///
/// BEREGNEDE KOLONNER ER IKKE MED: <c>Observation.YearCollected</c>,
/// <c>Observation.MonthCollected</c> og <c>Location.GeometryTypeId</c> beregnes av
/// databasen, og en innsetting i dem feiler.
///
/// PUBLISERINGEN SETTER RESTEN. Kolonnene som ikke står i kartet, men som er
/// <c>NOT NULL</c> i måltabellen, settes der: <c>CreatedAt</c>, <c>UpdatedAt</c>,
/// <c>IsDeleted</c> på alle tabellene som arver <c>BaseEntity</c>,
/// <c>Location.TimeStamp</c>, <c>ObservationError.DateTimeModified</c>,
/// <c>ObservationDetails.Id</c> (samme som observasjonen), og mediefilenes
/// <c>Size</c>, <c>Downloaded</c>, <c>InDownloadqueue</c> og
/// <c>DownloadRetryCount</c>, som alle er 0 for en nyskrevet rad.
///
/// ALLE STAGING-KOLONNER ER NULLBARE. Staging er et mellomlager, og
/// <c>NOT NULL</c>-kravene håndheves av måltabellene når publiseringen setter inn.
/// Faller en rad på et slikt krav, skal den feile der kolonnen faktisk er — ikke i
/// bulk-kopieringen, der feilmeldingen bare oppgir et kolonnenummer.
/// </summary>
public static class ImportStagingSchema
{
    /// <summary>
    /// SRID-en all geometri skrives med. Se <c>SearchConstants.DefaultEpsgCode</c> og
    /// migrasjonen som merket om de eksisterende radene.
    /// </summary>
    public const int Srid = 25833;

    private static readonly WKBWriter Wkb = new();

    /// <summary>
    /// Lokalitetene som ikke alt finnes. Publiseringen gjør <c>MERGE</c> på
    /// <c>LookupId</c>.
    /// </summary>
    public static StagingTable<ImportedLocation> Location { get; } = new(
        "Location",
        batch => batch.Locations,
        ("LookupId", "nvarchar(50)", x => x.LookupId),
        ("LocationId", "nvarchar(50)", x => x.LocationId),
        ("NodeId", "int", x => x.NodeId),
        ("Latitude", "float", x => x.Latitude),
        ("Longitude", "float", x => x.Longitude),
        ("East", "int", x => x.East),
        ("North", "int", x => x.North),
        ("CoordinatePrecision", "int", x => x.CoordinatePrecision),
        ("Locality", "nvarchar(1000)", x => x.Locality),
        ("GeometryWkb", "varbinary(max)", x => TilWkb(x.Geometry)));

    /// <summary>
    /// Områdene lokaliteten ligger i. Bare <c>AreaId</c> havner i tabellen;
    /// områdetypen og koden brukes av de avledede tabellene (fase 7).
    /// </summary>
    public static StagingTable<(ImportedLocation Location, LocationArea Area)> LocationAreas { get; } = new(
        "LocationAreas",
        batch => batch.Locations.SelectMany(x => x.Areas.Select(y => (Location: x, Area: y))),
        ("LookupId", "nvarchar(50)", x => x.Location.LookupId),
        ("AreaId", "int", x => x.Area.AreaId));

    /// <summary>
    /// Observasjonene. Publiseringen gjør <c>MERGE</c> på <c>ProxyId</c>.
    /// </summary>
    public static StagingTable<ImportedObservation> Observation { get; } = new(
        "Observation",
        batch => batch.Observations,
        ("ProxyId", "nvarchar(255)", x => x.ProxyId),
        ("NodeId", "int", x => x.NodeId),
        ("CatalogNumber", "nvarchar(200)", x => x.CatalogNumber),
        ("OccurrenceId", "nvarchar(255)", x => x.OccurrenceId),
        ("DateLastModified", "datetime2", x => x.DateLastModified),
        ("DateTimeCollected", "datetime2", x => x.DateTimeCollected),
        ("DatetimeIdentified", "datetime2", x => x.DatetimeIdentified),
        ("DateTimeRecordImported", "datetime2", x => x.DateTimeRecordImported),
        ("DateTimeRecordProcessed", "datetime2", x => x.DateTimeRecordProcessed),
        ("BasisOfRecordId", "int", x => x.BasisOfRecordId),
        ("TaxonId", "int", x => x.TaxonId),
        ("MatchedScientificNameId", "int", x => x.MatchedScientificNameId),
        ("TaxonGroupId", "int", x => x.TaxonGroupId),
        ("CategoryId", "int", x => x.CategoryId),
        ("Latitude", "float", x => x.Latitude),
        ("Longitude", "float", x => x.Longitude),
        ("East", "int", x => x.East),
        ("North", "int", x => x.North),
        ("CoordinatePrecisionInMeters", "int", x => x.CoordinatePrecisionInMeters),

        // Lokaliteten er kjent på forhånd når bufferet traff; ellers slås den opp på
        // LookupId i publiseringen.
        ("LocationId", "int", x => x.LocationId),
        ("LookupId", "nvarchar(50)", x => x.Location?.LookupId),

        ("HashCode", "int", x => x.HashCode),
        ("ProcessEngineId", "int", x => x.ProcessEngineId),
        ("HasErrors", "bit", x => x.HasErrors),
        ("HasAnnotations", "bit", x => x.HasAnnotations),
        ("HasMediaFiles", "bit", x => x.HasMediaFiles),
        ("ObservationQualityTypeId", "int", x => x.ObservationQualityTypeId),
        ("InstitutionOrgId", "int", x => x.InstitutionOrgId),
        ("DatasetOrgId", "int", x => x.DatasetOrgId),
        ("RegistrationStatusId", "tinyint", x => x.RegistrationStatusId),

        // KOLONNEN TAR ÉN ATFERD, LISTEN KAN HA FLERE. Målt 1:1 i dagens data (plan
        // 4.5 punkt 9), og koblingstabellen får alle uansett. Hva som skal skje med en
        // post som har to er åpent spørsmål 7.2 punkt 6.
        ("BehaviorId", "tinyint", x => FørsteAtferd(x)));

    /// <summary>
    /// Detaljradene. Samme Id som observasjonen — tabellene er delt, ikke relatert.
    /// </summary>
    public static StagingTable<ImportedObservation> ObservationDetails { get; } = new(
        "ObservationDetails",
        batch => batch.Observations,
        ("ProxyId", "nvarchar(255)", x => x.ProxyId),
        ("DatasetName", "nvarchar(1000)", x => x.DatasetName),
        ("DatasetId", "nvarchar(250)", x => x.DatasetId),
        ("Collector", "nvarchar(250)", x => x.Collector),
        ("IndividualCount", "nvarchar(100)", x => x.IndividualCount),
        ("Notes", "nvarchar(3500)", x => x.Notes),
        ("IdentifiedBy", "nvarchar(100)", x => x.IdentifiedBy),
        ("Locality", "nvarchar(100)", x => x.Locality),
        ("Habitat", "nvarchar(2000)", x => x.Habitat),
        ("Sex", "nvarchar(50)", x => x.Sex),
        ("DateTimeCollectedStr", "nvarchar(15)", x => x.DateTimeCollectedStr),

        // NOT NULL i tabellen, og en tom liste blir en tom streng — ikke null.
        ("AssociatedReferences", "nvarchar(max)", x => string.Join("|", x.AssociatedReferences)),

        ("CollectingMethod", "nvarchar(100)", x => x.CollectingMethod),
        ("RecordNumber", "nvarchar(50)", x => x.RecordNumber),
        ("FieldNumber", "nvarchar(100)", x => x.FieldNumber),
        ("MeasurementMethod", "nvarchar(100)", x => x.MeasurementMethod),
        ("GeoreferenceRemarks", "nvarchar(50)", x => x.GeoreferenceRemarks),
        ("Preparations", "nvarchar(200)", x => x.Preparations),
        ("OtherCatalogNumbers", "nvarchar(200)", x => x.OtherCatalogNumbers),
        ("RelatedResourceId", "nvarchar(50)", x => x.RelatedResourceId),
        ("RelationshipOfResource", "nvarchar(50)", x => x.RelationshipOfResource),
        ("TypeStatus", "nvarchar(50)", x => x.TypeStatus),
        ("EventTime", "nvarchar(100)", x => x.EventTime),
        ("MaximumElevationInMeters", "int", x => x.MaximumElevationInMeters),
        ("MinimumElevationInMeters", "int", x => x.MinimumElevationInMeters),
        ("VerbatimDepth", "nvarchar(100)", x => x.VerbatimDepth),
        ("DynamicProperties", "nvarchar(max)", x => x.DynamicProperties));

    public static StagingTable<(ImportedObservation Observation, int TagId)> ObservationTags { get; } = new(
        "ObservationTags",
        batch => batch.Observations.SelectMany(x => x.Tags.Select(y => (Observation: x, TagId: (int)y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("TagId", "int", x => x.TagId));

    /// <summary>
    /// Taksonene observasjonen knyttes til: selve taksonet og hele slektslinjen, slik
    /// et søk på «sopp» finner en post bestemt til art.
    /// </summary>
    public static StagingTable<(ImportedObservation Observation, int TaxonId)> ObservationTaxons { get; } = new(
        "ObservationTaxons",
        batch => batch.Observations.SelectMany(x => x.TaxonIds.Select(y => (Observation: x, TaxonId: y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("TaxonId", "int", x => x.TaxonId));

    public static StagingTable<(ImportedObservation Observation, byte BehaviorId)> ObservationBehaviors { get; } = new(
        "ObservationBehaviors",
        batch => batch.Observations.SelectMany(x => x.BehaviorIds.Select(y => (Observation: x, BehaviorId: y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("BehaviorId", "int", x => (int)x.BehaviorId));

    /// <summary>
    /// Prosjektene observasjonen hører til (<c>Organization</c> av type 3). n:m — målt
    /// 745 066 observasjoner med mer enn ett, maks 5.
    /// </summary>
    public static StagingTable<(ImportedObservation Observation, int ProjectOrgId)> ObservationProject { get; } = new(
        "ObservationProject",
        batch => batch.Observations.SelectMany(x => x.ProjectOrgIds.Select(y => (Observation: x, ProjectOrgId: y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("ProjectOrgId", "int", x => x.ProjectOrgId));

    /// <summary>
    /// Mediefilene. Importen laster ikke ned noe: <c>Size</c>, <c>Downloaded</c>,
    /// <c>InDownloadqueue</c> og <c>DownloadRetryCount</c> settes til 0 av
    /// publiseringen, slik Artskart 2 gjør, og nedlastingskøen tar resten.
    ///
    /// SAMME <c>Origin</c> TO GANGER PÅ EN POST BLIR ÉN RAD. Publiseringen differ
    /// mediefiler på <c>(Observation_Id, Origin)</c> — den må, fordi raden bærer den
    /// nedlastede filen — og to rader med samme origin kan da ikke skilles. Kilden kan
    /// sende dem: en URL kan stå både i <c>associatedMedia</c> og i
    /// multimedia-utvidelsen uten at identifikatorstrengene er like.
    /// </summary>
    public static StagingTable<(ImportedObservation Observation, ImportedMediaFile File)> MediaFile { get; } = new(
        "MediaFile",
        batch => batch.Observations.SelectMany(x => x.MediaFiles
            .GroupBy(y => y.Origin.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(y => (Observation: x, File: y.First()))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("Origin", "nvarchar(2000)", x => x.File.Origin),
        ("MediaFileTypeId", "int", x => x.File.MediaFileTypeId),
        ("License", "nvarchar(500)", x => x.File.License),
        ("RightsHolder", "nvarchar(500)", x => x.File.RightsHolder),
        ("Description", "nvarchar(500)", x => x.File.Description));

    public static StagingTable<(ImportedObservation Observation, ImportedObservationError Error)> ObservationError
    {
        get;
    } = new(
        "ObservationError",
        batch => batch.Observations.SelectMany(x => x.Errors.Select(y => (Observation: x, Error: y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("ValidationCode", "int", x => (int)x.Error.ValidationCode),
        ("ErrorContext", "nvarchar(400)", x => x.Error.ErrorContext),
        ("AnnotationId", "int", x => x.Error.AnnotationId));

    /// <summary>
    /// Koblinger til andre observasjoner. <c>LinkedObservationId</c> slås opp i
    /// publiseringen og blir <c>null</c> om den andre posten ikke finnes — den kan være
    /// i en senere bunt, eller i en node som ikke er importert.
    /// </summary>
    public static StagingTable<(ImportedObservation Observation, ImportedObservationLink Link)> ObservationLink
    {
        get;
    } = new(
        "ObservationLink",
        batch => batch.Observations.SelectMany(x => x.Links.Select(y => (Observation: x, Link: y))),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("LinkedProxyId", "nvarchar(255)", x => x.Link.LinkedProxyId),
        ("Remarks", "nvarchar(max)", x => x.Link.Remarks));

    /// <summary>
    /// De umaskerte verdiene for en sensitiv observasjon (fase 8). Observasjonen selv
    /// bærer den maskerte posisjonen.
    /// </summary>
    public static StagingTable<(ImportedObservation Observation, ImportedSensitiveData Data)> SensitiveObservationData
    {
        get;
    } = new(
        "SensitiveObservationData",
        batch => batch.Observations
            .Where(x => x.SensitiveData is not null)
            .Select(x => (Observation: x, Data: x.SensitiveData!)),
        ("ProxyId", "nvarchar(255)", x => x.Observation.ProxyId),
        ("Latitude", "float", x => x.Data.Latitude),
        ("Longitude", "float", x => x.Data.Longitude),
        ("East", "int", x => x.Data.East),
        ("North", "int", x => x.Data.North),
        ("CoordinatePrecisionInMeters", "int", x => x.Data.CoordinatePrecisionInMeters),
        ("Locality", "nvarchar(100)", x => x.Data.Locality),
        ("Notes", "nvarchar(3500)", x => x.Data.Notes),
        ("DateTimeCollected", "datetime2", x => x.Data.DateTimeCollected),
        ("GeometryWkb", "varbinary(max)", x => TilWkb(x.Data.Geometry)));

    /// <summary>
    /// Tabellene i publiseringsrekkefølge: lokaliteter først fordi observasjonene peker
    /// på dem, så observasjonene, så barna deres.
    ///
    /// MÅ STÅ ETTER tabellene den viser til: statiske initialiserere kjører i
    /// tekstrekkefølge, og står den først, er alle elementene null.
    /// </summary>
    public static IReadOnlyList<IStagingTable> All { get; } =
    [
        Location,
        LocationAreas,
        Observation,
        ObservationDetails,
        ObservationTags,
        ObservationTaxons,
        ObservationBehaviors,
        ObservationProject,
        MediaFile,
        ObservationError,
        ObservationLink,
        SensitiveObservationData
    ];

    /// <summary>
    /// Atferden som havner i kolonnen på observasjonen. Første i listen, som er
    /// rekkefølgen kilden oppgav dem i.
    /// </summary>
    private static byte? FørsteAtferd(ImportedObservation observation) =>
        observation.BehaviorIds.Count > 0 ? observation.BehaviorIds[0] : null;

    private static byte[]? TilWkb(Geometry? geometry) => geometry is null ? null : Wkb.Write(geometry);
}
