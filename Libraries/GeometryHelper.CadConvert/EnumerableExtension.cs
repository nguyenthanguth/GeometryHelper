using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.CommonGeometry.Extension;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Provides extension methods over collections of AutoCAD geometry.
    /// </summary>
    public static class EnumerableExtension
    {
        /// <summary>
        /// Builds the open chain of 2D segments running through the points, one segment per consecutive pair.
        /// <para>
        /// The chain is not closed: nothing joins the last point back to the first, so a ring has to repeat
        /// its first point at the end to come out closed. A list of n points yields n - 1 segments, and a
        /// list of fewer than two points yields none rather than failing.
        /// </para>
        /// <para>
        /// Coincident neighbours are not filtered, so a repeated point produces a zero length segment. Run
        /// <see cref="RemoveConsecutiveNearPoints(List{Point2d}, double)"/> first when that matters.
        /// </para>
        /// </summary>
        /// <param name="points">The points to connect, in order.</param>
        /// <returns>The segments joining each point to the one after it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<LineSegment2d> ToLineSegments2d(this List<Point2d> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var segments = new List<LineSegment2d>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment2d(points[i], points[i + 1]));
            }

            return segments;
        }

        /// <summary>
        /// Builds the open chain of 3D segments running through the points, one segment per consecutive pair.
        /// <para>
        /// The chain is not closed: nothing joins the last point back to the first, so a ring has to repeat
        /// its first point at the end to come out closed. A list of n points yields n - 1 segments, and a
        /// list of fewer than two points yields none rather than failing.
        /// </para>
        /// <para>
        /// Coincident neighbours are not filtered, so a repeated point produces a zero length segment. Run
        /// <see cref="RemoveConsecutiveNearPoints(List{Point3d}, double)"/> first when that matters.
        /// </para>
        /// </summary>
        /// <param name="points">The points to connect, in order.</param>
        /// <returns>The segments joining each point to the one after it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<LineSegment3d> ToLineSegments3d(this List<Point3d> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var segments = new List<LineSegment3d>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment3d(points[i], points[i + 1]));
            }

            return segments;
        }

        /// <summary>
        /// Thins a chain of 2D points by dropping every point that lands within <paramref name="tolerance"/>
        /// of the point kept before it.
        /// <para>
        /// The comparison is against the last point <c>kept</c>, not against the original neighbour, and
        /// that is what makes the result hold its promise: no two consecutive points of the returned list
        /// are within <paramref name="tolerance"/> of each other. A huddle of points collapses onto the
        /// first of them, and collapsing stops as soon as one point escapes the tolerance around that
        /// anchor, so a run is never swallowed whole however long it is.
        /// </para>
        /// <para>
        /// The first point always survives; the last one is not privileged. A final point lying within the
        /// tolerance of the one kept before it is dropped like any other, which pulls the end of the chain
        /// back slightly. That matters when the points are about to become a polyline whose endpoint is
        /// meaningful.
        /// </para>
        /// </summary>
        /// <param name="points">The points to thin, in order.</param>
        /// <param name="tolerance">
        /// The distance at or below which a point counts as coinciding with the previous kept one. A
        /// tolerance of zero removes only exactly repeated points.
        /// </param>
        /// <returns>
        /// A new list holding the surviving points in their original order. The input is left untouched,
        /// and a list of one point or none comes back as a copy.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<Point2d> RemoveConsecutiveNearPoints(this List<Point2d> points, double tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count <= 1)
            {
                return new List<Point2d>(points);
            }

            var result = new List<Point2d> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                if (result[result.Count - 1].GetDistanceTo(points[i]) > tolerance)
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Thins a chain of 3D points by dropping every point that lands within <paramref name="tolerance"/>
        /// of the point kept before it.
        /// <para>
        /// The comparison is against the last point <c>kept</c>, not against the original neighbour, and
        /// that is what makes the result hold its promise: no two consecutive points of the returned list
        /// are within <paramref name="tolerance"/> of each other. A huddle of points collapses onto the
        /// first of them, and collapsing stops as soon as one point escapes the tolerance around that
        /// anchor, so a run is never swallowed whole however long it is.
        /// </para>
        /// <para>
        /// Distance is measured in all three dimensions, so two points that share X and Y but differ in Z
        /// are not fused. The first point always survives; the last one is not privileged, so a final point
        /// lying within the tolerance of the one kept before it is dropped like any other.
        /// </para>
        /// </summary>
        /// <param name="points">The points to thin, in order.</param>
        /// <param name="tolerance">
        /// The distance at or below which a point counts as coinciding with the previous kept one. A
        /// tolerance of zero removes only exactly repeated points.
        /// </param>
        /// <returns>
        /// A new list holding the surviving points in their original order. The input is left untouched,
        /// and a list of one point or none comes back as a copy.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<Point3d> RemoveConsecutiveNearPoints(this List<Point3d> points, double tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count <= 1)
            {
                return new List<Point3d>(points);
            }

            var result = new List<Point3d> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                if (result[result.Count - 1].DistanceTo(points[i]) > tolerance)
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the longest of the 2D segments.
        /// <para>
        /// Length is the straight distance between the endpoints, so equal lengths keep the earlier
        /// segment, in list order.
        /// </para>
        /// </summary>
        /// <param name="segments">The segments to search. Must hold at least one segment.</param>
        /// <returns>The segment with the greatest length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="segments"/> is empty.</exception>
        public static LineSegment2d GetLongestLength(this List<LineSegment2d> segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            return segments.MaxBy(mb => mb.StartPoint.GetDistanceTo(mb.EndPoint));
        }

        /// <summary>
        /// Returns the longest of the 3D segments.
        /// <para>
        /// Length is measured in three dimensions, so of two segments that look equally long from above
        /// the sloped one wins. Equal lengths keep the earlier segment, in list order.
        /// </para>
        /// </summary>
        /// <param name="segments">The segments to search. Must hold at least one segment.</param>
        /// <returns>The segment with the greatest length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="segments"/> is empty.</exception>
        public static LineSegment3d GetLongestLength(this List<LineSegment3d> segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            return segments.MaxBy(mb => mb.StartPoint.DistanceTo(mb.EndPoint));
        }
    }
}
