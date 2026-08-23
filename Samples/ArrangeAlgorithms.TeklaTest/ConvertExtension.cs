using System;
using System.Collections.Generic;
using System.Linq;
using PlaneGeometry.Geometry;
using Tekla.Structures.Datatype;
using Tekla.Structures.Drawing;
using Tekla.Structures.Geometry3d;

namespace ArrangeAlgorithms.TeklaTest
{
    public static class ConvertExtension
    {
        public static GeoPoint2 ToGeoPoint(this Point point)
        {
            return new GeoPoint2(point.X, point.Y);
        }

        public static List<GeoPoint2> ToGeoPoints(this List<Point> points)
        {
            return points.Select(ToGeoPoint).ToList();
        }

        public static GeoVector2 ToGeoVector(this Vector vector)
        {
            return new GeoVector2(vector.X, vector.Y);
        }

        public static List<GeoVector2> ToGeoVectors(this List<Vector> vectors)
        {
            return vectors.Select(ToGeoVector).ToList();
        }

        public static Point ToTeklaPoint(this GeoPoint2 point)
        {
            return new Point(point.X, point.Y);
        }

        public static List<Point> ToTeklaPoints(this List<GeoPoint2> points)
        {
            return points.Select(ToTeklaPoint).ToList();
        }

        public static Vector ToTeklaVector(this GeoVector2 vector)
        {
            return new Vector(vector.X, vector.Y, 0.0);
        }

        public static List<Vector> ToTeklaVectors(this List<GeoVector2> vectors)
        {
            return vectors.Select(ToTeklaVector).ToList();
        }

        public static GeoLine2 ToGeoLine(this LineSegment lineSegment)
        {
            return new GeoLine2(lineSegment.StartPoint.ToGeoPoint(), lineSegment.EndPoint.ToGeoPoint());
        }

        public static GeoRectangle2 ToGeoRectangle(this AABB aabb)
        {
            return new GeoRectangle2(
                aabb.GetCenterPoint().ToGeoPoint(),
                aabb.MaxPoint.X - aabb.MinPoint.X,
                aabb.MaxPoint.Y - aabb.MinPoint.Y,
                0.0);
        }

        public static GeoRectangle2 ToGeoRectangle(this RectangleBoundingBox boundingBox)
        {
            return new GeoRectangle2(
                boundingBox.GetCenterPoint().ToGeoPoint(),
                boundingBox.Width,
                boundingBox.Height,
                Angle.FromDegrees(boundingBox.AngleToAxis).Radians);
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

        public static List<LineSegment> ToLineSegments(this List<Point> points)
        {
            List<LineSegment> segments = new List<LineSegment>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment(points[i], points[i + 1]));
            }

            return segments;
        }

        public static LineSegment GetLongestLength(this List<LineSegment> segments)
        {
            return segments.MaxBy(mb => mb.Length());
        }
    }
}
