using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting a curved chain in space, and keeping the bends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the question a bar is asked most often and the one it could not answer: a bar stopped at a pour
    /// break, or trimmed to the face of a member. <see cref="GeoPolyline3"/> can be cut twelve ways; a curved
    /// chain could not be cut at all, and turning it into a polyline first to cut it there throws away the
    /// thing that makes it a bar.
    /// </para>
    /// <para>
    /// So the pieces come back as chains of arcs. Cutting an arc gives two arcs of the same radius, which
    /// <see cref="GeoEdge3.TrySplitAtParameter(double, out GeoEdge3, out GeoEdge3)"/> already does, so the
    /// pieces put back end to end draw exactly what went in and each piece's length is still a length a bar
    /// schedule can use.
    /// </para>
    /// <para>
    /// A cut landing on a corner of the chain breaks it between the two edges rather than inside either, which
    /// is why the walk asks about the end of each edge separately: splitting at a parameter of nought or one
    /// is refused, and rightly, since it would leave a piece with nothing in it.
    /// </para>
    /// </remarks>
    internal static partial class ArcChain3
    {
        /// <summary>
        /// Cuts a run of edges wherever a set of points falls on it, and gathers the runs between the cuts.
        /// </summary>
        /// <param name="edges">The edges of the chain, in order.</param>
        /// <param name="at">The points to cut at; any that is not on the chain is passed over.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One run of edges per piece, in order along the chain.</returns>
        private static List<List<GeoEdge3>> CutRuns(IEnumerable<GeoEdge3> edges, IEnumerable<GeoPoint3> at, Tolerance tolerance)
        {
            var cuts = new List<GeoPoint3>(at);
            var runs = new List<List<GeoEdge3>>();
            var current = new List<GeoEdge3>();

            foreach (GeoEdge3 edge in edges)
            {
                var along = new List<double>();

                foreach (GeoPoint3 cut in cuts)
                {
                    if (!edge.IsPointOn(cut, tolerance))
                    {
                        continue;
                    }

                    double parameter = edge.GetParameterAtPoint(cut, tolerance);

                    // Nought and one are the ends of the edge, and those break between edges instead.
                    if (parameter > 0.0 && parameter < 1.0)
                    {
                        along.Add(parameter);
                    }
                }

                along.Sort();

                GeoEdge3 remaining = edge;
                double consumed = 0.0;

                foreach (double parameter in along)
                {
                    // Each cut is measured along what is left, not along the edge it started as.
                    double onRemaining = (parameter - consumed) / (1.0 - consumed);

                    if (onRemaining <= 0.0 || onRemaining >= 1.0)
                    {
                        continue;
                    }

                    if (!remaining.TrySplitAtParameter(onRemaining, out GeoEdge3 before, out GeoEdge3 after))
                    {
                        continue;
                    }

                    current.Add(before);
                    runs.Add(current);
                    current = new List<GeoEdge3>();
                    remaining = after;
                    consumed = parameter;
                }

                current.Add(remaining);

                // A cut sitting on the far end of this edge breaks the chain here, between the edges.
                foreach (GeoPoint3 cut in cuts)
                {
                    if (cut.IsEqualTo(edge.EndPoint, tolerance))
                    {
                        runs.Add(current);
                        current = new List<GeoEdge3>();
                        break;
                    }
                }
            }

            if (current.Count > 0)
            {
                runs.Add(current);
            }

            return runs;
        }

        /// <summary>
        /// Turns runs of edges into chains, dropping any that came out empty.
        /// </summary>
        private static GeoPolylineArc3[] AsChains(List<List<GeoEdge3>> runs)
        {
            var pieces = new List<GeoPolylineArc3>();

            foreach (List<GeoEdge3> run in runs)
            {
                if (run.Count > 0)
                {
                    pieces.Add(new GeoPolylineArc3(run));
                }
            }

            return pieces.ToArray();
        }

        /// <summary>
        /// Cuts a chain wherever a set of points falls on it.
        /// </summary>
        private static bool TryCut(GeoPolylineArc3 chain, IEnumerable<GeoPoint3> at, Tolerance tolerance, out GeoPolylineArc3[] pieces)
        {
            pieces = AsChains(CutRuns(chain.GetEdges(), at, tolerance));

            return pieces.Length > 1;
        }

        #region By a point and by a distance

        /// <summary>
        /// Cuts a chain at a point on it.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoPoint3 point, out GeoPolylineArc3[] pieces)
            => TrySplitBy(chain, point, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a chain at a point on it, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="point">The point to cut at; it has to be on the chain.</param>
        /// <param name="pieces">The pieces, in order along the chain, when the cut was made.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and the chain is handed back whole.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoPoint3 point, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (!chain.IsPointOn(point, tolerance))
            {
                pieces = new[] { chain };
                return false;
            }

            return TryCut(chain, new[] { point }, tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a chain at a distance measured along it.
        /// </summary>
        public static bool TrySplitAtDistance(GeoPolylineArc3 chain, double distance, out GeoPolylineArc3[] pieces)
            => TrySplitAtDistance(chain, distance, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a chain at a distance measured along it, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance">How far along to cut, measured along the arcs rather than across the chords.</param>
        /// <param name="pieces">The two pieces when the cut was made.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false.</returns>
        /// <remarks>
        /// The distance runs along the bar, so cutting a bar at its mid-length gives two pieces of the same
        /// length, which is not what cutting the set-out at its mid-length would give.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitAtDistance(GeoPolylineArc3 chain, double distance, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (double.IsNaN(distance) || distance <= 0.0 || distance >= chain.Length)
            {
                pieces = new[] { chain };
                return false;
            }

            return TryCut(chain, new[] { chain.GetPointAtDistance(distance) }, tolerance, out pieces);
        }

        #endregion

        #region By a plane, a flat region and a body

        /// <summary>
        /// Cuts a chain wherever it crosses a plane.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoPlane3 cutter, out GeoPolylineArc3[] pieces)
            => TrySplitBy(chain, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a chain wherever it crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutter">The plane to cut at.</param>
        /// <param name="pieces">The pieces, in order along the chain, when any cut was made.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false.</returns>
        /// <remarks>
        /// This is the pour break: a bar stopped at a construction joint comes back as two bars, each with its
        /// bends where they were and a length a schedule can use.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoPlane3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return TryCut(chain, GetIntersections(chain, cutter, tolerance), tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a chain wherever it crosses a face.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoFace3 cutter, out GeoPolylineArc3[] pieces)
            => TrySplitBy(chain, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a chain wherever it crosses a face, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutter">The face to cut at; only its material cuts, so a hole lets the chain through.</param>
        /// <param name="pieces">The pieces, in order along the chain, when any cut was made.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the face is null.</exception>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoFace3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return TryCut(chain, GetIntersections(chain, cutter, tolerance), tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a chain where it crosses the surface of a solid, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoSolid3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses the surface of a solid, telling what is in from what is out, within a
        /// tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutter">The body to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece rather than at an end, because every
        /// end is on the surface by construction and the surface belongs to neither side.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the body is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoSolid3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            GeoPolylineArc3[] pieces = AsChains(CutRuns(chain.GetEdges(), GetIntersections(chain, cutter, tolerance), tolerance));

            var within = new List<GeoPolylineArc3>();
            var without = new List<GeoPolylineArc3>();

            foreach (GeoPolylineArc3 piece in pieces)
            {
                if (Containment3.Contains(cutter, piece.GetPointAtDistance(piece.Length / 2.0), tolerance))
                {
                    within.Add(piece);
                }
                else
                {
                    without.Add(piece);
                }
            }

            inside = within.ToArray();
            outside = without.ToArray();

            return pieces.Length > 1;
        }

        #endregion
        #region Cutting a closed loop

        /// <summary>
        /// Cuts a closed loop wherever a set of points falls on it.
        /// </summary>
        /// <remarks>
        /// A loop wraps, so the run holding its start vertex is found in two halves — the tail of the walk and
        /// the head of it — and those are one piece. They are joined unless a cut lands on that vertex, which
        /// is the one case where the loop really does come apart there.
        /// <para>
        /// Cutting a closed loop gives open chains, so the pieces are <see cref="GeoPolylineArc3"/> and not
        /// loops. One cut leaves one chain: a ring cut once is a strip.
        /// </para>
        /// </remarks>
        private static GeoPolylineArc3[] CutLoop(GeoPolygonArc3 loop, IEnumerable<GeoPoint3> at, Tolerance tolerance)
        {
            var cuts = new List<GeoPoint3>(at);
            List<List<GeoEdge3>> runs = CutRuns(loop.GetEdges(), cuts, tolerance);

            if (runs.Count > 1)
            {
                bool atStart = false;

                foreach (GeoPoint3 cut in cuts)
                {
                    if (cut.IsEqualTo(loop[0], tolerance))
                    {
                        atStart = true;
                        break;
                    }
                }

                if (!atStart)
                {
                    List<GeoEdge3> tail = runs[runs.Count - 1];

                    runs.RemoveAt(runs.Count - 1);
                    tail.AddRange(runs[0]);
                    runs[0] = tail;
                }
            }

            return AsChains(runs);
        }

        /// <summary>
        /// Cuts a loop at a set of points, handing it back opened when nothing cut it.
        /// </summary>
        private static bool TryCutLoop(GeoPolygonArc3 loop, IEnumerable<GeoPoint3> at, Tolerance tolerance, out GeoPolylineArc3[] pieces)
        {
            var cuts = new List<GeoPoint3>(at);

            if (cuts.Count == 0)
            {
                pieces = new[] { loop.ToPolylineArc3() };
                return false;
            }

            pieces = CutLoop(loop, cuts, tolerance);

            return true;
        }

        /// <summary>
        /// Cuts a loop at a point on it.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoPoint3 point, out GeoPolylineArc3[] pieces)
            => TrySplitBy(loop, point, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a loop at a point on it, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="point">The point to cut at; it has to be on the loop.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the cut was made; otherwise, false, and the loop is handed back opened at its start.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoPoint3 point, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (!loop.IsPointOn(point, tolerance))
            {
                pieces = new[] { loop.ToPolylineArc3() };
                return false;
            }

            return TryCutLoop(loop, new[] { point }, tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a loop wherever it crosses a plane.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoPlane3 cutter, out GeoPolylineArc3[] pieces)
            => TrySplitBy(loop, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a loop wherever it crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutter">The plane to cut at.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        /// <remarks>
        /// A plane through a loop cuts it twice, so the usual answer is two chains.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoPlane3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return TryCutLoop(loop, GetIntersections(loop, cutter, tolerance), tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a loop wherever it crosses a face.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoFace3 cutter, out GeoPolylineArc3[] pieces)
            => TrySplitBy(loop, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a loop wherever it crosses a face, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutter">The face to cut at; only its material cuts.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoFace3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return TryCutLoop(loop, GetIntersections(loop, cutter, tolerance), tolerance, out pieces);
        }

        /// <summary>
        /// Cuts a loop where it crosses the surface of a solid, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoSolid3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses the surface of a solid, telling what is in from what is out, within a
        /// tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutter">The body to cut against.</param>
        /// <param name="inside">The chains within the material.</param>
        /// <param name="outside">The chains outside it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false, and the opened loop lands in one list or the other.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the body is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoSolid3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            bool cut = TryCutLoop(loop, GetIntersections(loop, cutter, tolerance), tolerance, out GeoPolylineArc3[] pieces);

            var within = new List<GeoPolylineArc3>();
            var without = new List<GeoPolylineArc3>();

            foreach (GeoPolylineArc3 piece in pieces)
            {
                if (Containment3.Contains(cutter, piece.GetPointAtDistance(piece.Length / 2.0), tolerance))
                {
                    within.Add(piece);
                }
                else
                {
                    without.Add(piece);
                }
            }

            inside = within.ToArray();
            outside = without.ToArray();

            return cut;
        }

        /// <summary>
        /// Cuts a loop at a set of distances measured along it.
        /// </summary>
        public static bool SplitAtDistances(GeoPolygonArc3 loop, IEnumerable<double> distances, out GeoPolylineArc3[] pieces)
            => SplitAtDistances(loop, distances, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a loop at a set of distances measured along it, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="distances">How far along to cut, measured along the arcs; anything outside the loop is passed over.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the list of distances is null.</exception>
        public static bool SplitAtDistances(GeoPolygonArc3 loop, IEnumerable<double> distances, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (distances == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            var at = new List<GeoPoint3>();

            foreach (double distance in distances)
            {
                if (!double.IsNaN(distance) && distance > 0.0 && distance < loop.Length)
                {
                    at.Add(loop.GetPointAtDistance(distance));
                }
            }

            return TryCutLoop(loop, at, tolerance, out pieces);
        }

        #endregion
    }
}
