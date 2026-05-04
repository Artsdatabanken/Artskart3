using System;
using System.Collections.Generic;
using System.Text;
using Artskart3.Core.Domain.BusinessModels;
using GeoJSON.Net.Feature;
using GeoJSON.Net.CoordinateReferenceSystem;
using GeoJSON.Net.Geometry;

namespace Artskart3.Core.Application.Converters
{
    public static class GeoJsonConverter
    {
        /// <summary>
        /// Default EPSG code for UTM Zone 33N
        /// </summary>
        private const int DefaultEpsg = 25833;

        public static async Task<string> LocationsToGeoJson(IAsyncEnumerable<LocationModel> locations, StyleType styleType = StyleType.Unknown, int? targetEpsg = null)
        {
            int featureCollectionEpsg = (int)(targetEpsg ?? DefaultEpsg);
            var features = new List<Feature>();

            await foreach (var location in locations)
            {
                if (location == null) continue;

                var properties = new Dictionary<string, object>
                {
                    { "ObservationCount", location.ObservationCount },
                    { "Locality", location.Locality ?? string.Empty }
                };

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

                features.Add(
                    CreateFeature(location, properties, location.Id.ToString(), featureCollectionEpsg)
                );
            }

            var featureCollection = new FeatureCollection(features)
            {
                CRS = new NamedCRS("EPSG:" + featureCollectionEpsg)
            };

            return GeoJsonWriter.ToGeoJson(featureCollection);
        }

        private static Feature CreateFeature(LocationModel location, Dictionary<string, object> properties, string id, int epsg)
        {
            IGeometryObject geometry;
            geometry = new Point(new Position(location.Latitude, location.Longitude));

            var feature = new Feature(geometry, properties, id)
            {
                CRS = new NamedCRS("EPSG:" + epsg)
            };

            return feature;
        }
    }
}
