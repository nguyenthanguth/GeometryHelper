using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Extension
{
    /// <summary>
    /// Provides extension methods over collections of 3D points.
    /// </summary>
    public static class GeoPointEnumerableExtension
    {
        /// <summary>
        /// Builds the open chain of line segments running through the points, one segment per consecutive pair.
        /// <para>
        /// The chain is not closed: nothing joins the last point back to the first, so a ring has to repeat
        /// its first point at the end to come out closed. A list of n points yields n - 1 segments, and a
        /// list of fewer than two points yields none rather than failing.
        /// </para>
        /// <para>
        /// Coincident neighbours are not filtered, so a repeated point produces a zero length segment. Run
        /// <see cref="RemoveConsecutiveNearPoints"/> first when that matters.
        /// </para>
        /// </summary>
        /// <param name="points">The points to connect, in order.</param>
        /// <returns>The segments joining each point to the one after it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<GeoLine3> ToGeoLine3s(this List<GeoPoint3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            List<GeoLine3> segments = new List<GeoLine3>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new GeoLine3(points[i], points[i + 1]));
            }

            return segments;
        }

        /// <summary>
        /// Thins a chain of points by dropping every point that lands within the tolerance of the point
        /// kept before it.
        /// <para>
        /// The comparison is against the last point kept, not against the original neighbour, and that is
        /// what makes the result hold its promise: no two consecutive points of the returned list are
        /// coincident within <paramref name="tolerance"/>. A huddle of points collapses onto the first of
        /// them, and collapsing stops as soon as one point escapes the tolerance around that anchor, so a
        /// run is never swallowed whole however long it is.
        /// </para>
        /// <para>
        /// The first point always survives; the last one is not privileged. A final point lying within the
        /// tolerance of the one kept before it is dropped like any other, which pulls the end of the chain
        /// back slightly. That matters when the points are about to become a polyline whose endpoint is
        /// meaningful.
        /// </para>
        /// <para>
        /// Coincidence is measured by <see cref="GeoPoint3.IsEqualTo(GeoPoint3, Tolerance)"/>, so it is
        /// <see cref="Tolerance.EqualPoint"/> that decides, and the same rule the polyline constructor
        /// applies when it filters its own vertices.
        /// </para>
        /// </summary>
        /// <param name="points">The points to thin, in order.</param>
        /// <param name="tolerance">The tolerance whose <see cref="Tolerance.EqualPoint"/> decides coincidence.</param>
        /// <returns>
        /// A new list holding the surviving points in their original order. The input is left untouched,
        /// and a list of one point or none comes back as a copy.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<GeoPoint3> RemoveConsecutiveNearPoints(this List<GeoPoint3> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count <= 1)
            {
                return new List<GeoPoint3>(points);
            }

            List<GeoPoint3> result = new List<GeoPoint3> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                if (!result[result.Count - 1].IsEqualTo(points[i], tolerance))
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }
    }
}
