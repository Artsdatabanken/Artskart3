using GeoJSON.Net.Geometry;
using Microsoft.SqlServer.Types;
using GeometryCollection = GeoJSON.Net.Geometry.GeometryCollection;
using LineString = GeoJSON.Net.Geometry.LineString;
using MultiLineString = GeoJSON.Net.Geometry.MultiLineString;
using MultiPoint = GeoJSON.Net.Geometry.MultiPoint;
using MultiPolygon = GeoJSON.Net.Geometry.MultiPolygon;
using Point = GeoJSON.Net.Geometry.Point;
using Polygon = GeoJSON.Net.Geometry.Polygon;
using Position = GeoJSON.Net.Geometry.Position;

namespace Artskart3.Core.Application.Converters
{
    public static class GeoJsonGeometry
    {
        private const string GEOMETRYCOLLECTION = "GeometryCollection";
        private const string LINESTRING = "LineString";
        private const string MULTILINESTRING = "MultiLineString";
        private const string MULTIPOINT = "MultiPoint";
        private const string MULTIPOLYGON = "MultiPolygon";
        private const string POINT = "Point";
        private const string POLYGON = "Polygon";

        public static IGeometryObject FromSqlGeometry(SqlGeometry geometry)
        {
            IGeometryObject geometryObject;

            switch (geometry.STGeometryType().Value)
            {
                case GEOMETRYCOLLECTION:
                    geometryObject = FromSqlGeometryCollection(geometry);
                    break;
                case LINESTRING:
                    geometryObject = FromSqlLineString(geometry);
                    break;
                case MULTILINESTRING:
                    geometryObject = FromSqlMultiLineString(geometry);
                    break;
                case MULTIPOINT:
                    geometryObject = FromSqlMultPoint(geometry);
                    break;
                case MULTIPOLYGON:
                    geometryObject = FromSqlMultiPolygon(geometry);
                    break;
                case POINT:
                    geometryObject = FromSqlPoint(geometry);
                    break;
                case POLYGON:
                    geometryObject = FromSqlPolygon(geometry);
                    break;
                default:
                    throw new Exception("Converting geometry failed. Unknown geometry type.");
            }

            return geometryObject;
        }

        private static Point FromSqlPoint(SqlGeometry point)
        {
            return new Point(GetPosition(point));
        }

        private static MultiPoint FromSqlMultPoint(SqlGeometry multiPoint)
        {
            List<Point> points = new List<Point>();

            for (int i = 1; i <= multiPoint.STNumGeometries(); ++i)
                points.Add(FromSqlPoint(multiPoint.STGeometryN(i)));

            return new MultiPoint(points);
        }

        private static LineString FromSqlLineString(SqlGeometry lineString)
        {
            List<Position> points = new List<Position>();

            for (int i = 1; i <= lineString.STNumPoints(); ++i)
                points.Add(GetPosition(lineString.STPointN(i)));

            return new LineString(points);
        }

        private static MultiLineString FromSqlMultiLineString(SqlGeometry multiLineString)
        {
            List<LineString> lineStrings = new List<LineString>();

            for (int i = 1; i <= multiLineString.STNumGeometries(); ++i)
                lineStrings.Add(FromSqlLineString(multiLineString.STGeometryN(i)));

            return new MultiLineString(lineStrings);
        }

        private static Polygon FromSqlPolygon(SqlGeometry polygon)
        {
            var lineStrings = new List<LineString>();
            var exteriorRing = polygon.STExteriorRing();

            if (!exteriorRing.IsNull)
                lineStrings.Add(FromSqlLineString(exteriorRing));

            for (int i = 1; i <= polygon.STNumInteriorRing(); ++i)
                lineStrings.Add(FromSqlLineString(polygon.STInteriorRingN(i)));

            return new Polygon(lineStrings);
        }

        private static MultiPolygon FromSqlMultiPolygon(SqlGeometry multiPolygon)
        {
            List<Polygon> polygons = new List<Polygon>();

            for (int i = 1; i <= multiPolygon.STNumGeometries(); ++i)
                polygons.Add(FromSqlPolygon(multiPolygon.STGeometryN(i)));

            return new MultiPolygon(polygons);
        }

        private static GeometryCollection FromSqlGeometryCollection(SqlGeometry geometryCollection)
        {
            List<IGeometryObject> geometries = new List<IGeometryObject>();

            for (int i = 1; i <= geometryCollection.STNumGeometries(); ++i)
                geometries.Add(FromSqlGeometry(geometryCollection.STGeometryN(i)));

            return new GeometryCollection(geometries);
        }

        private static Position GetPosition(SqlGeometry point)
        {
            if (point.HasZ)
                return new Position(
                    point.STX.Value,
                    point.STY.Value,
                    point.Z.Value
                );

            return new Position(
                point.STX.Value,
                point.STY.Value
            );
        }
    }
}
