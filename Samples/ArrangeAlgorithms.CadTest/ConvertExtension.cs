using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.CadTest
{
    public static class ConvertExtension
    {
        public static GeoPoint2 ToGeoPoint(this Point2d point)
        {
            return new GeoPoint2(point.X, point.Y);
        }

        public static List<GeoPoint2> ToGeoPoints(this List<Point2d> points)
        {
            return points.Select(ToGeoPoint).ToList();
        }

        public static GeoPoint2 ToGeoPoint(this Point3d point)
        {
            return new GeoPoint2(point.X, point.Y);
        }

        public static List<GeoPoint2> ToGeoPoints(this List<Point3d> points)
        {
            return points.Select(ToGeoPoint).ToList();
        }

        public static GeoVector2 ToGeoVector(this Vector2d vector)
        {
            return new GeoVector2(vector.X, vector.Y);
        }

        public static List<GeoVector2> ToGeoVectors(this List<Vector2d> vectors)
        {
            return vectors.Select(ToGeoVector).ToList();
        }

        public static GeoVector2 ToGeoVector(this Vector3d vector)
        {
            return new GeoVector2(vector.X, vector.Y);
        }

        public static List<GeoVector2> ToGeoVectors(this List<Vector3d> vectors)
        {
            return vectors.Select(ToGeoVector).ToList();
        }

        public static Point2d ToAcadPoint2d(this GeoPoint2 point)
        {
            return new Point2d(point.X, point.Y);
        }

        public static List<Point2d> ToAcadPoint2ds(this List<GeoPoint2> points)
        {
            return points.Select(ToAcadPoint2d).ToList();
        }

        public static Point3d ToAcadPoint3d(this GeoPoint2 point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        public static List<Point3d> ToAcadPoint3ds(this List<GeoPoint2> points)
        {
            return points.Select(ToAcadPoint3d).ToList();
        }

        public static Vector2d ToAcadVector2d(this GeoVector2 vector)
        {
            return new Vector2d(vector.X, vector.Y);
        }

        public static List<Vector2d> ToAcadVector2ds(this List<GeoVector2> vectors)
        {
            return vectors.Select(ToAcadVector2d).ToList();
        }

        public static Vector3d ToAcadVector3d(this GeoVector2 vector)
        {
            return new Vector3d(vector.X, vector.Y, 0.0);
        }

        public static List<Vector3d> ToAcadVector3ds(this List<GeoVector2> vectors)
        {
            return vectors.Select(ToAcadVector3d).ToList();
        }

        public static GeoLine2 ToGeoLine(this LineSegment2d lineSegment)
        {
            return new GeoLine2(lineSegment.StartPoint.ToGeoPoint(), lineSegment.EndPoint.ToGeoPoint());
        }

        public static GeoLine2 ToGeoLine(this LineSegment3d lineSegment)
        {
            return new GeoLine2(lineSegment.StartPoint.ToGeoPoint(), lineSegment.EndPoint.ToGeoPoint());
        }

        public static GeoLine2 ToGeoLine(this Line line)
        {
            return new GeoLine2(line.StartPoint.ToGeoPoint(), line.EndPoint.ToGeoPoint());
        }

        public static GeoRectangle2 ToGeoRectangle(this Extents2d extents)
        {
            double width = extents.MaxPoint.X - extents.MinPoint.X;
            double height = extents.MaxPoint.Y - extents.MinPoint.Y;
            var center = new GeoPoint2((extents.MinPoint.X + extents.MaxPoint.X) / 2.0, (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
            return new GeoRectangle2(center, width, height, 0.0);
        }

        public static GeoRectangle2 ToGeoRectangle(this Extents3d extents)
        {
            double width = extents.MaxPoint.X - extents.MinPoint.X;
            double height = extents.MaxPoint.Y - extents.MinPoint.Y;
            var center = new GeoPoint2((extents.MinPoint.X + extents.MaxPoint.X) / 2.0, (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
            return new GeoRectangle2(center, width, height, 0.0);
        }

        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer = null)
        {
            comparer = comparer ?? Comparer<TKey>.Default;
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Empty sequence");
            }

            TSource val = enumerator.Current;
            TKey y = selector(val);
            while (enumerator.MoveNext())
            {
                TSource current = enumerator.Current;
                TKey val2 = selector(current);
                if (comparer.Compare(val2, y) > 0)
                {
                    val = current;
                    y = val2;
                }
            }

            return val;
        }

        public static List<LineSegment2d> ToLineSegments2d(this List<Point2d> points)
        {
            var segments = new List<LineSegment2d>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment2d(points[i], points[i + 1]));
            }

            return segments;
        }

        public static List<LineSegment3d> ToLineSegments3d(this List<Point3d> points)
        {
            var segments = new List<LineSegment3d>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment3d(points[i], points[i + 1]));
            }

            return segments;
        }

        public static LineSegment2d GetLongestLength(this List<LineSegment2d> segments)
        {
            return segments.MaxBy(mb => mb.StartPoint.GetDistanceTo(mb.EndPoint));
        }

        public static LineSegment3d GetLongestLength(this List<LineSegment3d> segments)
        {
            return segments.MaxBy(mb => mb.StartPoint.DistanceTo(mb.EndPoint));
        }

        public static GeoPolyline2 ToGeoPolyline(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            var points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                points.Add(polyline.GetPoint2dAt(i).ToGeoPoint());
            }
            if (polyline.Closed && points.Count > 0)
            {
                points.Add(points[0]);
            }
            return new GeoPolyline2(points);
        }

        public static Polyline ToAcadPolyline(this GeoPolyline2 geoPolyline)
        {
            if (geoPolyline == null) throw new ArgumentNullException(nameof(geoPolyline));
            var result = new Polyline();
            for (int i = 0; i < geoPolyline.VertexCount; i++)
            {
                result.AddVertexAt(i, new Point2d(geoPolyline.Vertices[i].X, geoPolyline.Vertices[i].Y), 0.0, 0.0, 0.0);
            }
            return result;
        }

        public static Line ToAcadLine(this GeoLine2 geoLine)
        {
            if (geoLine == null) throw new ArgumentNullException(nameof(geoLine));
            return new Line(new Point3d(geoLine.StartPoint.X, geoLine.StartPoint.Y, 0.0), new Point3d(geoLine.EndPoint.X, geoLine.EndPoint.Y, 0.0));
        }

        public static GeoPolygon2 ToGeoPolygon(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            var points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                points.Add(polyline.GetPoint2dAt(i).ToGeoPoint());
            }
            return new GeoPolygon2(points);
        }

        public static GeoCircle2 ToGeoCircle(this Circle circle)
        {
            if (circle == null) throw new ArgumentNullException(nameof(circle));
            return new GeoCircle2(circle.Center.ToGeoPoint(), circle.Radius);
        }

        public static bool TryToGeoRectangle(this Polyline polyline, out GeoRectangle2 rect)
        {
            rect = default(GeoRectangle2);
            if (polyline == null) return false;

            if (polyline.NumberOfVertices != 4 || !polyline.Closed)
            {
                return false;
            }

            Point2d p0 = polyline.GetPoint2dAt(0);
            Point2d p1 = polyline.GetPoint2dAt(1);
            Point2d p2 = polyline.GetPoint2dAt(2);
            Point2d p3 = polyline.GetPoint2dAt(3);

            double d01 = p0.GetDistanceTo(p1);
            double d12 = p1.GetDistanceTo(p2);
            double d23 = p2.GetDistanceTo(p3);
            double d30 = p3.GetDistanceTo(p0);

            const double tol = 1e-4;
            if (Math.Abs(d01 - d23) > tol || Math.Abs(d12 - d30) > tol)
            {
                return false;
            }

            Vector2d v01 = p1 - p0;
            Vector2d v12 = p2 - p1;
            double dot = v01.X * v12.X + v01.Y * v12.Y;
            if (Math.Abs(dot) > tol * v01.Length * v12.Length)
            {
                return false;
            }

            Point2d centerAcad = new Point2d((p0.X + p2.X) / 2.0, (p0.Y + p2.Y) / 2.0);
            GeoPoint2 center = centerAcad.ToGeoPoint();

            double width = d01;
            double height = d12;
            double angle = Math.Atan2(v01.Y, v01.X);

            rect = new GeoRectangle2(center, width, height, angle);
            return true;
        }
    }
}