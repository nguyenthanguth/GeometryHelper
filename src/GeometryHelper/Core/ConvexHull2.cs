using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The smallest convex polygon holding a set of points in the plane.
    /// </summary>
    /// <remarks>
    /// Andrew's monotone chain: the points sorted along X, the lower and upper chains walked once each, a corner
    /// dropped whenever the chain would turn the wrong way or run straight on. A point within the point
    /// tolerance of the line through its neighbours is dropped as lying on it, so the hull has no corner that
    /// does not turn.
    /// </remarks>
    public static class ConvexHull2
    {
        /// <summary>
        /// Gets the convex hull of some points, using the default tolerance.
        /// </summary>
        public static GeoPolygon2 Of(IEnumerable<GeoPoint2> points) => Of(points, Tolerance.Global);

        /// <summary>
        /// Gets the convex hull of some points, within a tolerance.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="tolerance">The tolerance deciding whether a point lies on the line through two others.</param>
        /// <returns>The hull, counter-clockwise, its corners taken from the points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the points span no area: fewer than three, or all in one line.</exception>
        public static GeoPolygon2 Of(IEnumerable<GeoPoint2> points, Tolerance tolerance)
        {
            if (!TryOf(points, out GeoPolygon2 hull, tolerance))
            {
                throw new ArgumentException("The points span no area: there are fewer than three, or all lie in one line.", nameof(points));
            }

            return hull;
        }

        /// <summary>
        /// Tries to get the convex hull of some points, using the default tolerance.
        /// </summary>
        public static bool TryOf(IEnumerable<GeoPoint2> points, out GeoPolygon2 hull) => TryOf(points, out hull, Tolerance.Global);

        /// <summary>
        /// Tries to get the convex hull of some points, within a tolerance.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="hull">The hull, counter-clockwise; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance deciding whether a point lies on the line through two others.</param>
        /// <returns>false when the points span no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public static bool TryOf(IEnumerable<GeoPoint2> points, out GeoPolygon2 hull, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            hull = null;
            var sorted = new List<GeoPoint2>(points);

            if (sorted.Count < 3)
            {
                return false;
            }

            sorted.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

            var chain = new List<GeoPoint2>(sorted.Count + 1);

            // The lower chain left to right, then the upper chain back; each keeps only corners turning left.
            for (int pass = 0; pass < 2; pass++)
            {
                int floor = chain.Count;

                for (int k = 0; k < sorted.Count; k++)
                {
                    GeoPoint2 point = pass == 0 ? sorted[k] : sorted[sorted.Count - 1 - k];

                    while (chain.Count >= floor + 2 && !TurnsLeft(chain[chain.Count - 2], chain[chain.Count - 1], point, tolerance))
                    {
                        chain.RemoveAt(chain.Count - 1);
                    }

                    chain.Add(point);
                }

                // The last point of each chain is the first of the other.
                chain.RemoveAt(chain.Count - 1);
            }

            var corners = new List<GeoPoint2>(chain.Count);

            foreach (GeoPoint2 corner in chain)
            {
                if (corners.Count == 0 || !corners[corners.Count - 1].IsEqualTo(corner, tolerance))
                {
                    corners.Add(corner);
                }
            }

            while (corners.Count > 1 && corners[corners.Count - 1].IsEqualTo(corners[0], tolerance))
            {
                corners.RemoveAt(corners.Count - 1);
            }

            if (corners.Count < 3)
            {
                return false;
            }

            hull = new GeoPolygon2(corners);
            return true;
        }

        /// <summary>
        /// Determines whether the way from a through b to c turns left by more than the point tolerance: b stands
        /// off the line from a to c, on its right-hand side seen along it.
        /// </summary>
        private static bool TurnsLeft(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, Tolerance tolerance)
        {
            double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            double span = a.DistanceTo(c);

            // The cross product is twice the triangle's area; over the base a to c it is b's reach off that base.
            return span > 0.0 ? cross > tolerance.EqualPoint * span : cross > 0.0;
        }
    }
}
