﻿using Artskart3.Core.Domain.BusinessModels;
using GeoJSON.Net.Feature;
using GeoJSON.Net.CoordinateReferenceSystem;
using GeoJSON.Net.Geometry;
﻿using System.Text;
using System.Text.Json;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Converters;

public static class GeoJsonConverter
{
    public static class GeoJsonConverter
    {        
        private const int DefaultEpsg = 25833;
        private const string EpsgFormatPrefix = "EPSG:";

        public static async Task<string> LocationsToGeoJson(
            IAsyncEnumerable<LocationModel> locations, 
            StyleType styleType = StyleType.Unknown, 
            int? targetEpsg = null)
        {
            int epsgCode = targetEpsg ?? DefaultEpsg;
            var features = new List<Feature>();

            await foreach (var location in locations)
            {
                var properties = BuildLocationProperties(location, styleType);
                var feature = CreatePointFeatureFromLocation(location, properties, epsgCode);
                features.Add(feature);
            }

            var featureCollection = CreateFeatureCollection(features, epsgCode);
            return GeoJsonWriter.ToGeoJson(featureCollection);
        }

        /// <summary>
        /// Builds property dictionary containing base observation properties and style-specific properties.
        /// </summary>
        private static Dictionary<string, object> BuildLocationProperties(LocationModel location, StyleType styleType)
        {
            var properties = new Dictionary<string, object>
            {
                { "ObservationCount", location.ObservationCount },
                { "Locality", location.Locality ?? string.Empty },
                { "TaxonId", location.TaxonId ?? 0 }
            };

            AppendStyleSpecificProperties(properties, location, styleType);
            return properties;
        }

        /// <summary>
        /// Appends style-specific properties to the properties dictionary based on the style type.
        /// </summary>
        private static void AppendStyleSpecificProperties(
            Dictionary<string, object> properties, 
            LocationModel location, 
            StyleType styleType)
        {
            switch (styleType)
            {
                case StyleType.Category:
                    properties.Add("MaxCategory", location.MaxCategory);
                    break;
                
                case StyleType.Precision:
                    if (location.CoordinatePrecision.HasValue)
                        properties.Add("Precision", location.CoordinatePrecision.Value);
                    break;                
            }
        }

        /// <summary>
        /// Creates a GeoJSON Feature with Point geometry from location coordinates.
        /// </summary>
        private static Feature CreatePointFeatureFromLocation(
            LocationModel location, 
            Dictionary<string, object> properties, 
            int epsgCode)
        {
            var pointGeometry = new Point(new Position(location.Latitude, location.Longitude));
            var epsgCrs = new NamedCRS(FormatEpsgCode(epsgCode));

            return new Feature(pointGeometry, properties, location.Id.ToString())
            {
                CRS = epsgCrs
            };
        }

        /// <summary>
        /// Creates a FeatureCollection with proper CRS definition.
        /// </summary>
        private static FeatureCollection CreateFeatureCollection(List<Feature> features, int epsgCode)
        {
            return new FeatureCollection(features)
            {
                CRS = new NamedCRS(FormatEpsgCode(epsgCode))
            };
        }

        /// <summary>
        /// Formats an EPSG code with the standard "EPSG:" prefix.
        /// </summary>
        private static string FormatEpsgCode(int epsgCode)
            => $"{EpsgFormatPrefix}{epsgCode}";
    /// <summary>
    /// Default EPSG code for UTM Zone 33N
    /// </summary>
    private const int DefaultEpsg = 25833;

    public static async Task<string> LocationsToGeoJson(
        IAsyncEnumerable<LocationModel> locations,
        StyleType styleType = StyleType.Unknown,
        int? targetEpsg = null)
    {
        int featureCollectionEpsg = targetEpsg ?? DefaultEpsg;
        using var stream = new MemoryStream();
        await using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WritePropertyName("features");
        writer.WriteStartArray();

        await foreach (var location in locations)
        {
            if (location == null)
            {
                continue;
            }

            WriteLocationFeature(writer, location, styleType, featureCollectionEpsg);
        }
        
        writer.WriteEndArray();
        WriteCrs(writer, featureCollectionEpsg);
        writer.WriteString("type", "FeatureCollection");
        writer.WriteEndObject();
        await writer.FlushAsync();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    
    private static void WriteLocationFeature(
        Utf8JsonWriter writer,
        LocationModel location,
        StyleType styleType,
        int epsg)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("geometry");
        WritePointGeometry(writer, location);
        writer.WriteString("id", location.Id.ToString());
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteNumber("ObservationCount", location.ObservationCount);
        writer.WriteString("Locality", location.Locality ?? string.Empty);

        switch (styleType)
        {
            case StyleType.Category:
                writer.WriteNumber("MaxCategory", (int)location.MaxCategory);
                break;
            case StyleType.Precision:
                if (location.CoordinatePrecision.HasValue)
                {
                    writer.WriteNumber("Precision", location.CoordinatePrecision.Value);
                }
                break;

            case StyleType.Species:
                writer.WriteNumber("TaxonId", location.DominantTaxonId);
                break;
        }
        writer.WriteEndObject();
        WriteCrs(writer, epsg);
        writer.WriteString("type", "Feature");
        writer.WriteEndObject();
    }
    
    private static void WritePointGeometry(Utf8JsonWriter writer, LocationModel location)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("coordinates");
        writer.WriteStartArray();
        
        writer.WriteNumberValue(location.Longitude);
        writer.WriteNumberValue(location.Latitude);
        
        writer.WriteEndArray();
        writer.WriteString("type", "Point");
        writer.WriteEndObject();
    }
    
    private static void WriteCrs(Utf8JsonWriter writer, int epsg)
    {
        writer.WritePropertyName("crs");
        writer.WriteStartObject();

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("name", $"EPSG:{epsg}");
        writer.WriteEndObject();

        writer.WriteString("type", "Name");

        writer.WriteEndObject();
    }
}
}