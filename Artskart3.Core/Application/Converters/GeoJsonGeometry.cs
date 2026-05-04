using GeoJSON.Net.Geometry;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsMultiPoint = NetTopologySuite.Geometries.MultiPoint;
using NtsLineString = NetTopologySuite.Geometries.LineString;
using NtsMultiLineString = NetTopologySuite.Geometries.MultiLineString;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using NtsMultiPolygon = NetTopologySuite.Geometries.MultiPolygon;
using NtsGeometryCollection = NetTopologySuite.Geometries.GeometryCollection;
using GeoJsonPoint = GeoJSON.Net.Geometry.Point;
using GeoJsonMultiPoint = GeoJSON.Net.Geometry.MultiPoint;
using GeoJsonLineString = GeoJSON.Net.Geometry.LineString;
using GeoJsonMultiLineString = GeoJSON.Net.Geometry.MultiLineString;
using GeoJsonPolygon = GeoJSON.Net.Geometry.Polygon;
using GeoJsonMultiPolygon = GeoJSON.Net.Geometry.MultiPolygon;
using GeoJsonGeometryCollection = GeoJSON.Net.Geometry.GeometryCollection;
using GeoJsonPosition = GeoJSON.Net.Geometry.Position;

namespace Artskart3.Core.Application.Converters
{
    public static class GeoJsonGeometry
    {
        public static IGeometryObject FromNtsGeometry(NtsGeometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            return geometry.GeometryType switch
            {
                "Point" => FromNtsPoint((NtsPoint)geometry),
                "MultiPoint" => FromNtsMultiPoint((NtsMultiPoint)geometry),
                "LineString" => FromNtsLineString((NtsLineString)geometry),
                "MultiLineString" => FromNtsMultiLineString((NtsMultiLineString)geometry),
                "Polygon" => FromNtsPolygon((NtsPolygon)geometry),
                "MultiPolygon" => FromNtsMultiPolygon((NtsMultiPolygon)geometry),
                "GeometryCollection" => FromNtsGeometryCollection((NtsGeometryCollection)geometry),
                _ => throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported")
            };
        }

        private static GeoJsonPoint FromNtsPoint(NtsPoint point)
        {
            return new GeoJsonPoint(GetPosition(point));
        }

        private static GeoJsonMultiPoint FromNtsMultiPoint(NtsMultiPoint multiPoint)
        {
            var points = new List<GeoJsonPoint>();
            for (int i = 0; i < multiPoint.NumGeometries; i++)
            {
                points.Add(FromNtsPoint((NtsPoint)multiPoint.GetGeometryN(i)));
            }
            return new GeoJsonMultiPoint(points);
        }

        private static GeoJsonLineString FromNtsLineString(NtsLineString lineString)
        {
            var positions = new List<GeoJsonPosition>();
            for (int i = 0; i < lineString.NumPoints; i++)
            {
                positions.Add(GetPosition(lineString.GetPointN(i)));
            }
            return new GeoJsonLineString(positions);
        }

        private static GeoJsonMultiLineString FromNtsMultiLineString(NtsMultiLineString multiLineString)
        {
            var lineStrings = new List<GeoJsonLineString>();
            for (int i = 0; i < multiLineString.NumGeometries; i++)
            {
                lineStrings.Add(FromNtsLineString((NtsLineString)multiLineString.GetGeometryN(i)));
            }
            return new GeoJsonMultiLineString(lineStrings);
        }

        private static GeoJsonPolygon FromNtsPolygon(NtsPolygon polygon)
        {
            var lineStrings = new List<GeoJsonLineString>();
            
            var exteriorRing = polygon.ExteriorRing;
            if (exteriorRing != null)
            {
                lineStrings.Add(FromNtsLineString(exteriorRing));
            }

            for (int i = 0; i < polygon.NumInteriorRings; i++)
            {
                lineStrings.Add(FromNtsLineString(polygon.GetInteriorRingN(i)));
            }

            return new GeoJsonPolygon(lineStrings);
        }

        private static GeoJsonMultiPolygon FromNtsMultiPolygon(NtsMultiPolygon multiPolygon)
        {
            var polygons = new List<GeoJsonPolygon>();
            for (int i = 0; i < multiPolygon.NumGeometries; i++)
            {
                polygons.Add(FromNtsPolygon((NtsPolygon)multiPolygon.GetGeometryN(i)));
            }
            return new GeoJsonMultiPolygon(polygons);
        }

        private static GeoJsonGeometryCollection FromNtsGeometryCollection(NtsGeometryCollection geometryCollection)
        {
            var geometries = new List<IGeometryObject>();
            for (int i = 0; i < geometryCollection.NumGeometries; i++)
            {
                geometries.Add(FromNtsGeometry(geometryCollection.GetGeometryN(i)));
            }
            return new GeoJsonGeometryCollection(geometries);
        }

        private static GeoJsonPosition GetPosition(NtsPoint point)
        {
            var coord = point.Coordinate;
            if (!double.IsNaN(coord.Z))
            {
                return new GeoJsonPosition(coord.X, coord.Y, coord.Z);
            }
            return new GeoJsonPosition(coord.X, coord.Y);
        }
    }
}
