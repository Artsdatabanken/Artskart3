using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Register over alle kolonner som kan eksporteres, med visningsnavn og standardvalg.
/// </summary>
public class ExportColumnRegistry
{
    private static readonly List<ExportColumnDefinition> Columns =
    [
        // Observation-felter
        Col("Id", "Id", true),
        Col("ProxyId", "ProxyId"),
        Col("OccurrenceId", "OccurrenceId", true),
        Col("NodeId", "NodeId"),
        Col("InstitutionCode", "Institusjonskode", true),
        Col("CollectionCode", "Samlingskode", true),
        Col("CatalogNumber", "Katalognummer", true),
        Col("BasisOfRecordId", "Funntype-ID"),
        Col("DateTimeCollected", "Innsamlingsdato", true),
        Col("DatetimeIdentified", "Bestemmelsesdato"),
        Col("DateLastModified", "Sist endret"),
        Col("DateTimeRecordImported", "Importert"),
        Col("TaxonId", "Takson-ID", true),
        Col("MatchedScientificNameId", "Matchet vitenskapelig navn-ID"),
        Col("TaxonGroupId", "Taksongruppe-ID"),
        Col("CategoryId", "Kategori-ID"),
        Col("Latitude", "Breddegrad", true),
        Col("Longitude", "Lengdegrad", true),
        Col("East", "Øst (UTM33)"),
        Col("North", "Nord (UTM33)"),
        Col("CoordinatePrecisionInMeters", "Koordinatpresisjon (meter)", true),
        Col("YearCollected", "Innsamlingsår"),
        Col("MonthCollected", "Innsamlingsmåned"),
        Col("HasErrors", "Har feil"),
        Col("HasAnnotations", "Har annoteringer"),
        Col("ObservationQualityTypeId", "Kvalitetstype-ID"),

        // ObservationDetail-felter
        Col("Detail.DatasetName", "Datasett", true),
        Col("Detail.DatasetId", "Datasett-ID"),
        Col("Detail.Collector", "Innsamler", true),
        Col("Detail.IndividualCount", "Antall individer"),
        Col("Detail.Notes", "Merknader"),
        Col("Detail.IdentifiedBy", "Bestemt av"),
        Col("Detail.Locality", "Lokalitet", true),
        Col("Detail.Habitat", "Habitat"),
        Col("Detail.Sex", "Kjønn"),
        Col("Detail.CollectingMethod", "Innsamlingsmetode"),
        Col("Detail.RecordNumber", "Postnummer"),
        Col("Detail.FieldNumber", "Feltnummer"),
        Col("Detail.MeasurementMethod", "Målemetode"),
        Col("Detail.GeoreferenceRemarks", "Georeferanse-merknader"),
        Col("Detail.Preparations", "Preparering"),
        Col("Detail.OtherCatalogNumbers", "Andre katalognumre"),
        Col("Detail.TypeStatus", "Typestatus"),
        Col("Detail.EventTime", "Hendelsestid"),
        Col("Detail.MaximumElevationInMeters", "Maks høyde (meter)"),
        Col("Detail.MinimumElevationInMeters", "Min høyde (meter)"),
        Col("Detail.VerbatimDepth", "Dybde (tekst)"),
        Col("Detail.DynamicProperties", "Dynamiske egenskaper"),
        Col("Detail.AssociatedReferences", "Tilknyttede referanser"),
    ];

    public IReadOnlyList<ExportColumnDefinition> GetAllColumns() => Columns;

    public List<string> GetDefaultColumnNames() =>
        Columns.Where(c => c.IsDefaultSelected).Select(c => c.Name).ToList();

    public HashSet<string> GetValidColumnNames() =>
        Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Henter verdien for en gitt kolonne fra en observasjon.
    /// </summary>
    public static object? GetValue(Observation observation, string columnName)
    {
        return columnName switch
        {
            "Id" => observation.Id,
            "ProxyId" => observation.ProxyId,
            "OccurrenceId" => observation.OccurrenceId,
            "NodeId" => observation.NodeId,
            "InstitutionCode" => observation.InstitutionCode,
            "CollectionCode" => observation.CollectionCode,
            "CatalogNumber" => observation.CatalogNumber,
            "BasisOfRecordId" => observation.BasisOfRecordId,
            "DateTimeCollected" => observation.DateTimeCollected,
            "DatetimeIdentified" => observation.DatetimeIdentified,
            "DateLastModified" => observation.DateLastModified,
            "DateTimeRecordImported" => observation.DateTimeRecordImported,
            "TaxonId" => observation.TaxonId,
            "MatchedScientificNameId" => observation.MatchedScientificNameId,
            "TaxonGroupId" => observation.TaxonGroupId,
            "CategoryId" => observation.CategoryId,
            "Latitude" => observation.Latitude,
            "Longitude" => observation.Longitude,
            "East" => observation.East,
            "North" => observation.North,
            "CoordinatePrecisionInMeters" => observation.CoordinatePrecisionInMeters,
            "YearCollected" => observation.YearCollected,
            "MonthCollected" => observation.MonthCollected,
            "HasErrors" => observation.HasErrors,
            "HasAnnotations" => observation.HasAnnotations,
            "ObservationQualityTypeId" => observation.ObservationQualityTypeId,

            // Detail-felter
            "Detail.DatasetName" => observation.ObservationDetail?.DatasetName,
            "Detail.DatasetId" => observation.ObservationDetail?.DatasetId,
            "Detail.Collector" => observation.ObservationDetail?.Collector,
            "Detail.IndividualCount" => observation.ObservationDetail?.IndividualCount,
            "Detail.Notes" => observation.ObservationDetail?.Notes,
            "Detail.IdentifiedBy" => observation.ObservationDetail?.IdentifiedBy,
            "Detail.Locality" => observation.ObservationDetail?.Locality,
            "Detail.Habitat" => observation.ObservationDetail?.Habitat,
            "Detail.Sex" => observation.ObservationDetail?.Sex,
            "Detail.CollectingMethod" => observation.ObservationDetail?.CollectingMethod,
            "Detail.RecordNumber" => observation.ObservationDetail?.RecordNumber,
            "Detail.FieldNumber" => observation.ObservationDetail?.FieldNumber,
            "Detail.MeasurementMethod" => observation.ObservationDetail?.MeasurementMethod,
            "Detail.GeoreferenceRemarks" => observation.ObservationDetail?.GeoreferenceRemarks,
            "Detail.Preparations" => observation.ObservationDetail?.Preparations,
            "Detail.OtherCatalogNumbers" => observation.ObservationDetail?.OtherCatalogNumbers,
            "Detail.TypeStatus" => observation.ObservationDetail?.TypeStatus,
            "Detail.EventTime" => observation.ObservationDetail?.EventTime,
            "Detail.MaximumElevationInMeters" => observation.ObservationDetail?.MaximumElevationInMeters,
            "Detail.MinimumElevationInMeters" => observation.ObservationDetail?.MinimumElevationInMeters,
            "Detail.VerbatimDepth" => observation.ObservationDetail?.VerbatimDepth,
            "Detail.DynamicProperties" => observation.ObservationDetail?.DynamicProperties,
            "Detail.AssociatedReferences" => observation.ObservationDetail?.AssociatedReferences,

            _ => null
        };
    }

    private static ExportColumnDefinition Col(string name, string displayName, bool isDefault = false) =>
        new() { Name = name, DisplayName = displayName, IsDefaultSelected = isDefault };
}
