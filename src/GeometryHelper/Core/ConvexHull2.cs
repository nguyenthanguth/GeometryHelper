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
    /// dropped whenever the chain would turn the wrong way or run straight on, judged exactly, so that no corner of
    /// the hull is ever lost to the tolerance. The tolerance then takes out the corners that do not matter: a corner
    /// within it of the segment between its neighbours, and one within it of the corner before. No point stands more
    /// than the tolerance outside the hull that is left, however many corners go. A set thinner than the tolerance may
    /// still get a hull, because the tolerance is spent corner by corner and may run out before the corners are gone; a
    /// set that lies within the tolerance of a segment, all of it, may have none. Points that differ in X by a
    /// trillionth and in Y by 15 still make their corner: a hull of (0, 0), (1E-12, -15.8), (2E-12, -6.25) and (20, 0)
    /// is a triangle of 158 square units.
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

                    while (chain.Count >= floor + 2 && Cross(chain[chain.Count - 2], chain[chain.Count - 1], point) <= 0.0)
                    {
                        chain.RemoveAt(chain.Count - 1);
                    }

                    chain.Add(point);
                }

                // The last point of each chain is the first of the other.
                chain.RemoveAt(chain.Count - 1);
            }

            // The corners of the hull as the points stand, none turning the wrong way or straight on. Corners within the
            // tolerance of each other are one, and a corner within it of the segment between its neighbours is not one.
            var corners = new List<GeoPoint2>(chain.Count);
            var reach = new List<double>(chain.Count);

            foreach (GeoPoint2 corner in chain)
            {
                if (corners.Count > 0 && corners[corners.Count - 1].IsEqualTo(corner, tolerance))
                {
                    reach[reach.Count - 1] = Math.Max(reach[reach.Count - 1], corners[corners.Count - 1].DistanceTo(corner));
                }
                else
                {
                    corners.Add(corner);
                    reach.Add(0.0);
                }
            }

            // The last corner joins the first only if the points it stands for, too, lie within the tolerance of the first.
            while (corners.Count > 1 && corners[corners.Count - 1].IsEqualTo(corners[0], tolerance)
                && corners[0].DistanceTo(corners[corners.Count - 1]) + reach[reach.Count - 1] <= tolerance.EqualPoint)
            {
                reach[0] = Math.Max(reach[0], corners[0].DistanceTo(corners[corners.Count - 1]) + reach[reach.Count - 1]);
                corners.RemoveAt(corners.Count - 1);
                reach.RemoveAt(reach.Count - 1);
            }

            Simplify(corners, reach, tolerance.EqualPoint);

            if (corners.Count < 3)
            {
                return false;
            }

            // Built as they are: the public constructor would filter the corners again against the global tolerance, and
            // at a finer one drop corners that are real or throw.
            hull = new GeoPolygon2(corners.ToArray(), corners.Count);
            return true;
        }

        /// <summary>
        /// Twice the signed area of the triangle a, b, c: positive when the way from a through b to c turns left.
        /// </summary>
        private static double Cross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        /// <summary>
        /// Takes out of a convex polygon the corners that stand within a tolerance of the segment between their
        /// neighbours, one sweep after another until a sweep takes none.
        /// </summary>
        /// <param name="corners">The corners, counter-clockwise; the ones that stay.</param>
        /// <param name="reach">For each corner, how far from it the points merged into it lie.</param>
        /// <param name="tolerance">The most any point may stand outside the polygon that is left.</param>
        /// <remarks>
        /// A corner goes only if every point it stands for, those merged into it and the corners taken out before it that
        /// its edges carried, still lies within the tolerance of the edge that replaces them: how far the corners already
        /// taken out stand is added to how far the next one stands off its neighbours, so a run of corners each a little off
        /// the line does not wear the polygon away by more than the tolerance in all.
        /// </remarks>
        private static void Simplify(List<GeoPoint2> corners, List<double> reach, double tolerance)
        {
            int n = corners.Count;

            // A ring of corners by index, each linked to the one before and the one after, so that taking one out costs nothing;
            // and how far the points carried by the edge from each corner to the next stand off it.
            var before = new int[n];
            var after = new int[n];
            var gone = new bool[n];
            var edge = new double[n];

            for (int i = 0; i < n; i++)
            {
                before[i] = (i + n - 1) % n;
                after[i] = (i + 1) % n;
            }

            int left = n;
            bool taken = true;

            while (taken && left > 2)
            {
                taken = false;

                for (int i = 0; i < n && left > 2; i++)
                {
                    if (gone[i])
                    {
                        continue;
                    }

                    int b = before[i], a = after[i];
                    double carried = Math.Max(reach[i], Math.Max(edge[b], edge[i])) + DistanceToSegment(corners[i], corners[b], corners[a]);

                    if (carried <= tolerance)
                    {
                        edge[b] = carried;
                        after[b] = a;
                        before[a] = b;
                        gone[i] = true;
                        left--;
                        taken = true;
                    }
                }
            }

            var kept = new List<GeoPoint2>(left);

            for (int i = 0; i < n; i++)
            {
                if (!gone[i])
                {
                    kept.Add(corners[i]);
                }
            }

            corners.Clear();
            corners.AddRange(kept);
        }

        private static double DistanceToSegment(GeoPoint2 p, GeoPoint2 a, GeoPoint2 b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double length2 = dx * dx + dy * dy;
            double t = length2 > 0.0 ? Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length2)) : 0.0;

            return p.DistanceTo(new GeoPoint2(a.X + t * dx, a.Y + t * dy));
        }
    }
}
