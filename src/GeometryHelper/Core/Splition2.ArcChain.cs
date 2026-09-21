using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting a chain or a loop that may curve, along the arcs rather than across them.
    /// </summary>
    public static partial class Splition2
    {
        #region Chains that may curve

        /// <summary>
        /// Cuts a chain that may curve at distances along it.
        /// </summary>
        /// <param name="chain">The chain to cut.</param>
        /// <param name="distances">How far along to cut, in drawing units; the order does not matter, and a distance at either end or off the chain is ignored.</param>
        /// <returns>The pieces, in order; the chain itself when nothing was cut.</returns>
        /// <remarks>
        /// A cut inside an arc leaves two arcs of the same radius, not two chords, so the pieces put back
        /// end to end draw exactly what went in.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the distances are null.</exception>
        public static GeoPolylineArc2[] SplitAtDistances(GeoPolylineArc2 chain, IEnumerable<double> distances)
        {
            return SplitAtDistances(chain, distances, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve at distances along it, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the distances are null.</exception>
        public static GeoPolylineArc2[] SplitAtDistances(GeoPolylineArc2 chain, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (distances == null) throw new ArgumentNullException(nameof(distances));

            List<List<GeoEdge2>> runs = ArcChain2.SplitAt(ArcChain2.EdgesOf(chain), distances, tolerance);
            var pieces = new GeoPolylineArc2[runs.Count];

            for (int i = 0; i < runs.Count; i++)
            {
                pieces[i] = new GeoPolylineArc2(runs[i]);
            }

            return pieces;
        }

        /// <summary>
        /// Cuts a loop that may curve at distances round it.
        /// </summary>
        /// <param name="loop">The loop to cut.</param>
        /// <param name="distances">How far round to cut, in drawing units.</param>
        /// <returns>The pieces as open chains; an empty array when nothing was cut, because a loop cut nowhere is still the loop.</returns>
        /// <remarks>
        /// A loop has no start of its own, so the vertex it happens to be held from is not a cut: the run
        /// after the last cut carries on through it into the run before the first one.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the distances are null.</exception>
        public static GeoPolylineArc2[] SplitAtDistances(GeoPolygonArc2 loop, IEnumerable<double> distances)
        {
            return SplitAtDistances(loop, distances, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a loop that may curve at distances round it, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the distances are null.</exception>
        public static GeoPolylineArc2[] SplitAtDistances(GeoPolygonArc2 loop, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (distances == null) throw new ArgumentNullException(nameof(distances));

            List<List<GeoEdge2>> runs = ArcChain2.SplitAt(ArcChain2.EdgesOf(loop), distances, tolerance);

            if (runs.Count < 2)
            {
                return new GeoPolylineArc2[0];
            }

            // Where the loop was held from is not a cut, so the last run runs on into the first.
            List<GeoEdge2> last = runs[runs.Count - 1];
            runs.RemoveAt(runs.Count - 1);
            last.AddRange(runs[0]);
            runs[0] = last;

            var pieces = new GeoPolylineArc2[runs.Count];

            for (int i = 0; i < runs.Count; i++)
            {
                pieces[i] = new GeoPolylineArc2(runs[i]);
            }

            return pieces;
        }

        /// <summary>
        /// Cuts a chain that may curve in two at a distance along it.
        /// </summary>
        /// <param name="chain">The chain to cut.</param>
        /// <param name="distance">How far along to cut, in drawing units.</param>
        /// <param name="first">The piece from the start of the chain to the cut.</param>
        /// <param name="second">The piece from the cut to the end of the chain.</param>
        /// <returns>true when the chain was cut in two; false when the distance falls at an end or off it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitAtDistance(GeoPolylineArc2 chain, double distance, out GeoPolylineArc2 first, out GeoPolylineArc2 second)
        {
            return TrySplitAtDistance(chain, distance, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve in two at a distance along it, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitAtDistance(GeoPolylineArc2 chain, double distance, out GeoPolylineArc2 first, out GeoPolylineArc2 second, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            first = chain;
            second = chain;

            GeoPolylineArc2[] pieces = SplitAtDistances(chain, new[] { distance }, tolerance);

            if (pieces.Length != 2)
            {
                return false;
            }

            first = pieces[0];
            second = pieces[1];
            return true;
        }

        /// <summary>
        /// Cuts a chain that may curve in two at a point on it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPoint2 point, out GeoPolylineArc2 first, out GeoPolylineArc2 second)
        {
            return TrySplitBy(chain, point, out first, out second, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve in two at a point on it, within a tolerance.
        /// </summary>
        /// <returns>true when the chain was cut; false when the point is not on it, or is one of its ends.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPoint2 point, out GeoPolylineArc2 first, out GeoPolylineArc2 second, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            first = chain;
            second = chain;

            if (!Containment2.IsPointOn(chain, point, tolerance))
            {
                return false;
            }

            return TrySplitAtDistance(chain, Parametrization2.GetDistanceAtPoint(chain, point, tolerance), out first, out second, tolerance);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoLine2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(chain, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when the chain was cut anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoLine2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(chain, Intersection2.GetIntersections(chain, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoArc2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(chain, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets an arc, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoArc2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(chain, Intersection2.GetIntersections(chain, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPolygon2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(chain, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a straight loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPolygon2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(chain, Intersection2.GetIntersections(chain, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPolygonArc2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(chain, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve wherever it meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPolygonArc2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(chain, Intersection2.GetIntersections(chain, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a loop that may curve wherever it meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc2 loop, GeoLine2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(loop, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a loop that may curve wherever it meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when the loop was cut into two or more chains; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc2 loop, GeoLine2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(loop, Intersection2.GetIntersections(loop, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a loop that may curve wherever it meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc2 loop, GeoPolygon2 cutter, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(loop, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a loop that may curve wherever it meets a straight loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the cutter is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc2 loop, GeoPolygon2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(loop, Intersection2.GetIntersections(loop, cutter, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain that may curve at points on it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the points are null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPoint2[] points, out GeoPolylineArc2[] pieces)
        {
            return TrySplitBy(chain, points, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Cuts a chain that may curve at points on it, within a tolerance.
        /// </summary>
        /// <remarks>A point that is not on the chain is ignored rather than moved onto it.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the points are null.</exception>
        public static bool TrySplitBy(GeoPolylineArc2 chain, GeoPoint2[] points, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            return TrySplitAt(chain, points, out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain at the points of it that a set of points names, ignoring any that are not on it.
        /// </summary>
        private static bool TrySplitAt(GeoPolylineArc2 chain, GeoPoint2[] points, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            pieces = new[] { chain };

            var distances = new List<double>();

            foreach (GeoPoint2 point in points)
            {
                if (Containment2.IsPointOn(chain, point, tolerance))
                {
                    distances.Add(Parametrization2.GetDistanceAtPoint(chain, point, tolerance));
                }
            }

            if (distances.Count == 0)
            {
                return false;
            }

            GeoPolylineArc2[] cut = SplitAtDistances(chain, distances, tolerance);

            if (cut.Length < 2)
            {
                return false;
            }

            pieces = cut;
            return true;
        }

        /// <summary>
        /// Cuts a loop at the points of it that a set of points names, ignoring any that are not on it.
        /// </summary>
        private static bool TrySplitAt(GeoPolygonArc2 loop, GeoPoint2[] points, out GeoPolylineArc2[] pieces, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            pieces = new GeoPolylineArc2[0];

            var distances = new List<double>();

            foreach (GeoPoint2 point in points)
            {
                if (Containment2.IsPointOn(loop, point, tolerance))
                {
                    distances.Add(Parametrization2.GetDistanceAtPoint(loop, point, tolerance));
                }
            }

            if (distances.Count < 2)
            {
                // One cut leaves a loop opened up rather than two pieces, which is not a split.
                return false;
            }

            GeoPolylineArc2[] cut = SplitAtDistances(loop, distances, tolerance);

            if (cut.Length < 2)
            {
                return false;
            }

            pieces = cut;
            return true;
        }

        #endregion
    }
}
