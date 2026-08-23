using GeometryHelper.CommonGeometry.Extension;
using System;
using System.Collections.Generic;
using Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods over collections of Tekla geometry.
    /// </summary>
    public static class EnumerableExtension
    {
        /// <summary>
        /// Builds the open chain of segments running through the points, one segment per consecutive pair.
        /// <para>
        /// The chain is not closed: nothing joins the last point back to the first, so a polygon has to
        /// repeat its first point at the end to come out closed. A list of n points yields n - 1 segments,
        /// and a list of fewer than two points yields none rather than failing.
        /// </para>
        /// <para>
        /// Coincident neighbours are not filtered, so a repeated point produces a zero length segment. Run
        /// <see cref="RemoveConsecutiveNearPoints"/> first when that matters.
        /// </para>
        /// </summary>
        /// <param name="points">The points to connect, in order.</param>
        /// <returns>The segments joining each point to the one after it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<LineSegment> ToLineSegments(this List<Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            List<LineSegment> segments = new List<LineSegment>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new LineSegment(points[i], points[i + 1]));
            }

            return segments;
        }

        /// <summary>
        /// Thins a chain of points by dropping every point that lands within <paramref name="tolerance"/>
        /// of the point kept before it.
        /// <para>
        /// The comparison is against the last point <c>kept</c>, not against the original neighbour, and
        /// that is what makes the result hold its promise: no two consecutive points of the returned list
        /// are within <paramref name="tolerance"/> of each other. A huddle of points collapses onto the
        /// first of them, and collapsing stops as soon as one point escapes the tolerance around that
        /// anchor — so a run is never swallowed whole, however long it is.
        /// </para>
        /// <para>
        /// The first point always survives; the last one is not privileged. A final point lying within the
        /// tolerance of the one kept before it is dropped like any other, which pulls the end of the chain
        /// back slightly. That matters when the points are about to become a polyline whose endpoint is
        /// meaningful, and it is the one case where the caller may want to re-append the original last
        /// point afterwards.
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
        public static List<Point> RemoveConsecutiveNearPoints(this List<Point> points, double tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count <= 1)
            {
                return new List<Point>(points);
            }

            List<Point> result = new List<Point> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                if (Distance.PointToPoint(result[result.Count - 1], points[i]) > tolerance)
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the longest of the segments.
        /// <para>
        /// Length is the Tekla segment's own, so it is measured in three dimensions even when the caller
        /// only cares about the plan view: of two segments that look equally long from above, the sloped
        /// one wins. Equal lengths keep the earlier segment, in list order.
        /// </para>
        /// </summary>
        /// <param name="segments">The segments to search. Must hold at least one segment.</param>
        /// <returns>The segment with the greatest length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="segments"/> is empty.</exception>
        public static LineSegment GetLongestLength(this List<LineSegment> segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            return segments.MaxBy(mb => mb.Length());
        }
    }
}
