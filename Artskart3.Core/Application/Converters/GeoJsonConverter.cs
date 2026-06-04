using Artskart3.Core.Domain.BusinessModels;
using GeoJSON.Net.Feature;
using GeoJSON.Net.CoordinateReferenceSystem;
using GeoJSON.Net.Geometry;

namespace Artskart3.Core.Application.Converters
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
                
                case StyleType.Species:
                    properties.Add("TaxonId", location.DominantTaxonId);
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
            var pointGeometry = new Point(new Position(location.Longitude, location.Latitude));
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
    }
}
