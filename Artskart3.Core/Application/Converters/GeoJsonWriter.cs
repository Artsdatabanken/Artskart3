using GeoJSON.Net;
using GeoJSON.Net.CoordinateReferenceSystem;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System.Text;

namespace Artskart3.Core.Application.Converters
{
    public class GeoJsonWriter : JsonTextWriter
    {
        private static readonly int TaskCount = Environment.ProcessorCount;

        private GeoJsonWriter(TextWriter writer) : base(writer)
        {
        }

        public static string ToGeoJson(FeatureCollection featureCollection)
        {
            StringWriter stringWriter = new StringWriter();
            using (GeoJsonWriter writer = new GeoJsonWriter(stringWriter))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("features");
                writer.WriteStartArray();

                if (featureCollection.Features.Count < 100000)
                    writer.WriteFeatures(featureCollection);
                else
                    writer.WriteFeaturesMultithreaded(featureCollection);

                writer.WriteEndArray();

                if (featureCollection.CRS != null)
                    writer.WriteCrs((CRSBase)featureCollection.CRS);

                writer.WritePropertyName("type");
                writer.WriteValue("FeatureCollection");

                writer.WriteEndObject();

                stringWriter.Flush();
                return stringWriter.ToString();
            }
        }

 
        private void WriteFeaturesMultithreaded(FeatureCollection featureCollection)
        {
            int taskSize = featureCollection.Features.Count / TaskCount;

            Task<string>[] tasks = new Task<string>[TaskCount];
            for (int i = 0; i < TaskCount; ++i)
            {
                int fromIndex = i * taskSize;
                int toIndex;
                if (i != TaskCount - 1)
                    toIndex = fromIndex + taskSize;
                else
                    toIndex = featureCollection.Features.Count;

                tasks[i] =
                    Task<string>.Factory.StartNew(
                        () => AddFeatures(featureCollection.Features, fromIndex, toIndex));
            }

            for (int i = 0; i < TaskCount; ++i)
            {
                tasks[i].Wait();
                WriteRaw(tasks[i].Result);
            }
        }

        private void WriteFeatures(FeatureCollection featureCollection)
        {
            WriteRaw(AddFeatures(featureCollection.Features, 0, featureCollection.Features.Count));
        }

        private static string AddFeatures(IReadOnlyList<Feature> features, int fromIndex, int toIndex)
        {
            StringWriter stringWriter = new StringWriter();
            using (GeoJsonWriter writer = new GeoJsonWriter(stringWriter))
            {
                writer.WriteFeatures(features, fromIndex, toIndex);

                stringWriter.Flush();
                return stringWriter.ToString();
            }
        }

        private void WriteFeatures(IReadOnlyList<Feature> features, int fromIndex, int toIndex)
        {
            for (int i = fromIndex; i < toIndex; ++i)
            {
                WriteFeature(features[i]);

                if (i != features.Count - 1)
                    WriteRaw(",");
            }
        }

        private void WriteFeature(Feature feature)
        {
            WriteStartObject();
            WritePropertyName("geometry");

            WriteGeometry(feature.Geometry);

            if (feature.Id != null)
            {
                WritePropertyName("id");
                WriteValue(feature.Id);
            }

            if (feature.Properties.Count > 0)
            {
                WritePropertyName("properties");
                WriteStartObject();
                foreach (var property in feature.Properties)
                {
                    WritePropertyName(property.Key);
                    WriteValue(property.Value);
                }
                WriteEndObject();
            }
            if (feature.CRS != null)
            {
                WriteCrs((CRSBase)feature.CRS);
            }

            WritePropertyName("type");
            WriteValue("Feature");

            WriteEndObject();
        }

        private void WriteGeometry(IGeometryObject geometry)
        {
            switch (geometry.Type)
            {
                case GeoJSONObjectType.GeometryCollection:
                    WriteGeometryCollection((GeometryCollection)geometry);
                    break;
                case GeoJSONObjectType.Point:
                    WritePoint((Point)geometry);
                    break;
                case GeoJSONObjectType.MultiPoint:
                    WriteMulitPoint((MultiPoint)geometry);
                    break;
                case GeoJSONObjectType.LineString:
                    WriteLineString((LineString)geometry);
                    break;
                case GeoJSONObjectType.MultiLineString:
                    WriteMultiLineString((MultiLineString)geometry);
                    break;
                case GeoJSONObjectType.Polygon:
                    WritePolygon((Polygon)geometry);
                    break;
                case GeoJSONObjectType.MultiPolygon:
                    WriteMultiPolygon((MultiPolygon)geometry);
                    break;
                default:
                    throw new Exception("ERROR: Unknown GeoJson type.");
            }
        }

        private void WriteGeometryCollection(GeometryCollection geometryCollection)
        {
            WriteStartObject();
            WritePropertyName("geometries");
            WriteStartArray();

            foreach (var geometry in geometryCollection.Geometries)
                WriteGeometry(geometry);

            WriteEndArray();
            WritePropertyName("type");
            WriteValue("GeometryCollection");
            WriteEndObject();
        }

        private void WritePoint(Point point)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteCoordinate(point.Coordinates);
            WritePropertyName("type");
            WriteValue("Point");
            WriteEndObject();
        }

        private void WriteMulitPoint(MultiPoint multiPoint)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteStartArray();
            foreach (var point in multiPoint.Coordinates)
                WriteCoordinate(point.Coordinates);
            WriteEndArray();
            WritePropertyName("type");
            WriteValue("MultiPoint");
            WriteEndObject();
        }

        private void WriteLineString(LineString lineString)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteCoordinates(lineString.Coordinates);
            WritePropertyName("type");
            WriteValue("LineString");
            WriteEndObject();
        }

        private void WriteMultiLineString(MultiLineString multiLineString)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteStartArray();
            foreach (var lineString in multiLineString.Coordinates)
                WriteCoordinates(lineString.Coordinates);
            WriteEndArray();
            WritePropertyName("type");
            WriteValue("MultiLineString");
            WriteEndObject();
        }

        private void WritePolygon(Polygon polygon)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteStartArray();
            foreach (var lineString in polygon.Coordinates)
                WriteCoordinates(lineString.Coordinates);
            WriteEndArray();
            WritePropertyName("type");
            WriteValue("Polygon");
            WriteEndObject();
        }

        private void WriteMultiPolygon(MultiPolygon multiPolygon)
        {
            WriteStartObject();
            WritePropertyName("coordinates");
            WriteStartArray();
            foreach (var polygon in multiPolygon.Coordinates)
            {
                WriteStartArray();
                foreach (var lineString in polygon.Coordinates)
                    WriteCoordinates(lineString.Coordinates);
                WriteEndArray();
            }
            WriteEndArray();
            WritePropertyName("type");
            WriteValue("MultiPolygon");
            WriteEndObject();
        }

        private void WriteCoordinates(IEnumerable<IPosition> coordinates)
        {
            WriteStartArray();
            foreach (var coordinate in coordinates)
                WriteCoordinate(coordinate);
            WriteEndArray();
        }

        private void WriteCoordinate(IPosition coordinate)
        {
            var position = (Position)coordinate;
            WriteStartArray();
            WriteValue(position.Longitude);
            WriteValue(position.Latitude);
            if (position.Altitude.HasValue)
            {
                WriteValue(position.Altitude.Value);
            }
            WriteEndArray();
        }

        private void WriteCrs(CRSBase crs)
        {
            WritePropertyName("crs");
            WriteStartObject();
            WritePropertyName("properties");
            WriteStartObject();
            WritePropertyName("name");
            WriteValue(crs.Properties["name"]);
            WriteEndObject();
            WritePropertyName("type");
            WriteValue("Name");
            WriteEndObject();
        }
    }
}
