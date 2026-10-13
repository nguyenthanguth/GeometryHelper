using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Rounding the corners of a chain in space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what a reinforcing bar is made of: straight runs with a tangent arc at every bend, one
    /// bending radius, and the bends free to lie in whatever planes they like. A corner between two straight
    /// runs is flat whether or not the chain around it is, so each is rounded in its own plane, by the same
    /// arithmetic <see cref="Corner2"/> uses, and the answer is lifted back. Nothing is approximated.
    /// </para>
    /// <para>
    /// The rule for two corners wanting the same edge is the one the plane already uses, and it carries over
    /// untouched because it is about lengths along edges and not about dimensions: an edge can give away no
    /// more than it is long, counting both ends, and where it cannot the corner taking more of it gives way.
    /// </para>
    /// <para>
    /// Only a corner between two <b>straight</b> pieces is rounded. A leg that already curves lies in a plane
    /// of its own, which need not be the plane of the corner, so there is no one plane to do the arithmetic
    /// in; such a corner is left alone rather than answered approximately.
    /// </para>
    /// </remarks>
    public static partial class Corner3
    {
        /// <summary>
        /// Rounds every corner of a straight chain by the same radius.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolyline3 polyline, double radius) => Fillet(polyline, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of a straight chain by the same radius, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radius">The radius of every corner; a bending radius, in the language of a bar.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with its corners rounded; a corner with too little edge, or none to turn, or whose arc would be too small for the tolerance to tell its two ends apart, is left as it was.</returns>
        /// <remarks>
        /// Where the bends at a leg take all of it, give or take the tolerance, nothing of the leg is left and the
        /// arcs either side meet midway along it; the bar's two ends never move. The bar (0, 0, 0), (10, 0, 0),
        /// (20, 0.7, 0) bent at 1E-4 at the default tolerance keeps its bend square: the arc would touch the legs
        /// 3.5E-6 either side of it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public static GeoPolylineArc3 Fillet(GeoPolyline3 polyline, double radius, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Fillet(new GeoPolylineArc3(polyline), radius, tolerance);
        }

        /// <summary>
        /// Rounds every corner of a chain that may curve by the same radius.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolylineArc3 chain, double radius) => Fillet(chain, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of a chain that may curve by the same radius, within a tolerance.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolylineArc3 chain, double radius, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            RequirePositive(radius, nameof(radius));

            return Reshape(chain, Everywhere(radius, chain.EdgeCount), tolerance);
        }

        /// <summary>
        /// Rounds the corners of a straight chain, each by its own radius.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolyline3 polyline, IReadOnlyList<double> radii) => Fillet(polyline, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a straight chain, each by its own radius, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radii">The radius wanted at each vertex, read the way the bulges are read: the entry at an index belongs to the vertex at that index. A radius of nought leaves that corner alone, and a list shorter than the chain leaves the rest of it alone.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the radii are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is negative or not a number.</exception>
        public static GeoPolylineArc3 Fillet(GeoPolyline3 polyline, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Fillet(new GeoPolylineArc3(polyline), radii, tolerance);
        }

        /// <summary>
        /// Rounds the corners of a chain that may curve, each by its own radius.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolylineArc3 chain, IReadOnlyList<double> radii) => Fillet(chain, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a chain that may curve, each by its own radius, within a tolerance.
        /// </summary>
        public static GeoPolylineArc3 Fillet(GeoPolylineArc3 chain, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            RequireRadii(radii);

            return Reshape(chain, ByCorner(radii, chain.EdgeCount, chain.VertexCount), tolerance);
        }

        /// <summary>
        /// Rounds one named corner of a straight chain.
        /// </summary>
        public static bool TryFilletAt(GeoPolyline3 polyline, int index, double radius, out GeoPolylineArc3 result)
            => TryFilletAt(polyline, index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one named corner of a straight chain, within a tolerance.
        /// </summary>
        public static bool TryFilletAt(GeoPolyline3 polyline, int index, double radius, out GeoPolylineArc3 result, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return TryFilletAt(new GeoPolylineArc3(polyline), index, radius, out result, tolerance);
        }

        /// <summary>
        /// Rounds one named corner of a chain that may curve.
        /// </summary>
        public static bool TryFilletAt(GeoPolylineArc3 chain, int index, double radius, out GeoPolylineArc3 result)
            => TryFilletAt(chain, index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one named corner of a chain that may curve, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="index">Which vertex to round; the two ends have no corner and are refused.</param>
        /// <param name="radius">The radius of the corner.</param>
        /// <param name="result">The rounded chain, or the chain unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room to be rounded; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain or the radius is not a positive number.</exception>
        public static bool TryFilletAt(GeoPolylineArc3 chain, int index, double radius, out GeoPolylineArc3 result, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            if (index < 0 || index >= chain.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(radius, nameof(radius));

            result = chain;

            // The ends of a chain are not corners: nothing turns there.
            if (index == 0 || index == chain.VertexCount - 1)
            {
                return false;
            }

            if (!TryRound(chain.GetEdgeAt(index - 1), chain.GetEdgeAt(index), radius, tolerance, out Rounded rounded))
            {
                return false;
            }

            GeoEdge3 incoming = chain.GetEdgeAt(index - 1);
            GeoEdge3 outgoing = chain.GetEdgeAt(index);

            // A bend that takes the whole of a leg, give or take the tolerance, leaves nothing of it, and the arc
            // runs from the leg's far end, which does not move.
            bool allOfFirst = incoming.Length - rounded.Before.EndPoint.DistanceTo(incoming.EndPoint) <= tolerance.EqualPoint;
            bool allOfSecond = outgoing.Length - rounded.After.StartPoint.DistanceTo(outgoing.StartPoint) <= tolerance.EqualPoint;

            var edges = new List<GeoEdge3>();

            for (int e = 0; e < chain.EdgeCount; e++)
            {
                if (e == index - 1)
                {
                    if (!allOfFirst)
                    {
                        edges.Add(rounded.Before);
                    }

                    edges.Add(Moved(rounded.Arc, allOfFirst, incoming.StartPoint, allOfSecond, outgoing.EndPoint, tolerance));

                    if (!allOfSecond)
                    {
                        edges.Add(rounded.After);
                    }

                    e++;
                    continue;
                }

                edges.Add(chain.GetEdgeAt(e));
            }

            result = new GeoPolylineArc3(edges);
            return true;
        }

        /// <summary>
        /// Rounds the corners a list of radii asks for, giving way where an edge is too short to serve both ends.
        /// </summary>
        private static GeoPolylineArc3 Reshape(GeoPolylineArc3 chain, double?[] radiusAtCorner, Tolerance tolerance)
        {
            var edges = ArcChain3.EdgesOf(chain);
            int count = edges.Count;

            var arcAt = new GeoEdge3[count];
            var doCut = new bool[count];
            var cutFrom = new GeoPoint3[count];
            var cutTo = new GeoPoint3[count];
            var takenAtStart = new double[count];
            var takenAtEnd = new double[count];

            int refused = 0;

            for (int c = 0; c < count - 1; c++)
            {
                double? radius = radiusAtCorner[c];

                if (!radius.HasValue)
                {
                    continue;
                }

                if (!TryRound(edges[c], edges[c + 1], radius.Value, tolerance, out Rounded rounded))
                {
                    refused++;
                    continue;
                }

                arcAt[c] = rounded.Arc;
                doCut[c] = true;
                cutFrom[c] = rounded.Before.EndPoint;
                cutTo[c] = rounded.After.StartPoint;

                // Measured from the corner, so that a touching point beyond the far end of a leg, by up to the
                // tolerance, counts as taking more of the leg rather than less.
                takenAtEnd[c] = rounded.Before.EndPoint.DistanceTo(edges[c].EndPoint);
                takenAtStart[c + 1] = rounded.After.StartPoint.DistanceTo(edges[c + 1].StartPoint);
            }

            // An edge can only give away as much as it is long, counting both of its ends.
            bool again = true;

            while (again)
            {
                again = false;

                for (int e = 0; e < count; e++)
                {
                    // Exactly enough is enough: the dust left by working the tangent points out must not
                    // decide otherwise.
                    if (takenAtStart[e] + takenAtEnd[e] <= edges[e].Length + tolerance.EqualPoint)
                    {
                        continue;
                    }

                    int atStart = e - 1;
                    int atEnd = e;

                    int victim = takenAtEnd[e] >= takenAtStart[e] ? atEnd : atStart;

                    if (victim < 0 || victim >= count || !doCut[victim])
                    {
                        victim = victim == atEnd ? atStart : atEnd;
                    }

                    if (victim < 0 || victim >= count || !doCut[victim])
                    {
                        break;
                    }

                    doCut[victim] = false;
                    takenAtEnd[victim] = 0.0;
                    takenAtStart[victim + 1] = 0.0;
                    refused++;
                    again = true;
                }
            }

            if (refused > 0)
            {
                GeometryHelperLog.Debug($"Fillet left {refused} corner(s) in space alone: too little edge, no turn, or a leg that curves.");
            }

            return new GeoPolylineArc3(Assemble(edges, doCut, cutFrom, cutTo, arcAt, takenAtStart, takenAtEnd, tolerance));
        }

        /// <summary>
        /// Walks the legs of an open chain, shortening each end a corner took something from and putting the
        /// corner's new piece in, and gives back the edges of the result.
        /// </summary>
        /// <remarks>
        /// Where the corners at a leg take all of it, give or take the tolerance, nothing of it is left, and a stub
        /// running back along it is no answer: three sides of a square of side 10 bent at 5.02 at a tolerance of
        /// 0.1 left stubs that the chain refused, and threw. The pieces either side meet at one point instead: the
        /// leg's own end where only one of its corners was reshaped, so the ends of the chain and a corner left
        /// alone never move, and midway between the two where both were. A straight leg of no length that no corner
        /// touched goes too, as it always has, and the corner after it stays.
        /// </remarks>
        internal static List<GeoEdge3> Assemble(
            List<GeoEdge3> edges,
            bool[] doCut,
            GeoPoint3[] cutFrom,
            GeoPoint3[] cutTo,
            GeoEdge3[] newPiece,
            double[] takenAtStart,
            double[] takenAtEnd,
            Tolerance tolerance)
        {
            int count = edges.Count;

            var pieces = new List<GeoEdge3>(count * 2);
            var gone = new List<bool>(count * 2);
            var meetAt = new List<GeoPoint3>(count * 2);

            for (int e = 0; e < count; e++)
            {
                GeoEdge3 edge = edges[e];
                bool cutBefore = e > 0 && doCut[e - 1];

                GeoPoint3 start = cutBefore ? cutTo[e - 1] : edge.StartPoint;
                GeoPoint3 end = doCut[e] ? cutFrom[e] : edge.EndPoint;

                pieces.Add(Remade(edge, start, end));

                if (cutBefore || doCut[e])
                {
                    gone.Add(edge.Length - takenAtStart[e] - takenAtEnd[e] <= tolerance.EqualPoint);
                    meetAt.Add(!doCut[e] ? edge.EndPoint : !cutBefore ? edge.StartPoint : Midway(start, end));
                }
                else
                {
                    gone.Add(!edge.IsArc && edge.StartPoint.IsEqualTo(edge.EndPoint, tolerance));
                    meetAt.Add(edge.EndPoint);
                }

                if (doCut[e])
                {
                    // A cut no longer than the tolerance is no edge at that tolerance and goes, its two ends meeting
                    // where it starts; an arc stays.
                    pieces.Add(newPiece[e]);
                    gone.Add(!newPiece[e].IsArc && newPiece[e].StartPoint.IsEqualTo(newPiece[e].EndPoint, tolerance));
                    meetAt.Add(newPiece[e].StartPoint);
                }
            }

            int n = pieces.Count;

            if (!gone.Contains(true) || !gone.Contains(false))
            {
                return pieces;
            }

            var starts = new GeoPoint3?[n];
            var ends = new GeoPoint3?[n];
            int position = 0;

            while (position < n)
            {
                if (!gone[position])
                {
                    position++;
                    continue;
                }

                int runStart = position;

                while (position < n && gone[position])
                {
                    position++;
                }

                // A run at the start or the end of the chain meets at the chain's own end, which never moves.
                GeoPoint3 meet = runStart == 0 ? pieces[0].StartPoint
                               : position == n ? pieces[n - 1].EndPoint
                               : meetAt[position - 1];

                if (runStart > 0)
                {
                    ends[runStart - 1] = meet;
                }

                if (position < n)
                {
                    starts[position] = meet;
                }
            }

            var result = new List<GeoEdge3>(n);

            for (int k = 0; k < n; k++)
            {
                if (!gone[k])
                {
                    result.Add(Moved(pieces[k], starts[k].HasValue, starts[k] ?? default(GeoPoint3), ends[k].HasValue, ends[k] ?? default(GeoPoint3), tolerance));
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the piece a corner was reshaped into, its ends moved to where the legs either side vanished.
        /// </summary>
        /// <remarks>
        /// An arc keeps its bulge and its plane: the point it moves to lies on a leg, and both legs lie in the
        /// plane of the corner.
        /// </remarks>
        private static GeoEdge3 Moved(GeoEdge3 piece, bool movedStart, GeoPoint3 newStart, bool movedEnd, GeoPoint3 newEnd, Tolerance tolerance)
        {
            if (!movedStart && !movedEnd)
            {
                return piece;
            }

            GeoPoint3 start = movedStart ? newStart : piece.StartPoint;
            GeoPoint3 end = movedEnd ? newEnd : piece.EndPoint;

            // An arc squeezed to a chord the tolerance cannot tell from a point has no chord left to bulge along,
            // and what is left of the corner is the straight piece between the two.
            if (!piece.IsArc || !start.GetVectorTo(end).TryGetNormal(out _, tolerance))
            {
                return new GeoEdge3(start, end);
            }

            return new GeoEdge3(start, end, piece.Bulge, piece.Normal, tolerance);
        }

        /// <summary>
        /// Gets the point half way between two points.
        /// </summary>
        private static GeoPoint3 Midway(GeoPoint3 first, GeoPoint3 second)
            => new GeoPoint3((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5, (first.Z + second.Z) * 0.5);

        /// <summary>
        /// Rounds one corner between two straight pieces, in the plane the corner itself lies in.
        /// </summary>
        /// <remarks>
        /// The two legs and the vertex between them are always flat, whatever the chain does elsewhere, so
        /// they are laid out in a frame of their own, rounded by <see cref="ArcFillet2"/>, and lifted back.
        /// The arc carries that frame away with it as the plane its bulge is read in.
        /// </remarks>
        private static bool TryRound(GeoEdge3 incoming, GeoEdge3 outgoing, double radius, Tolerance tolerance, out Rounded rounded)
        {
            rounded = default(Rounded);

            // A leg that curves lies in a plane of its own, which need not be the plane of this corner.
            if (incoming.IsArc || outgoing.IsArc)
            {
                return false;
            }

            GeoPoint3 corner = incoming.EndPoint;

            // Two legs running straight on have no corner to round, and name no plane either. The frame is built
            // from the unit legs and the plane the tolerance finds them to span, so that it cannot call the legs
            // parallel where the tolerance did not: a bend of 1E-7 radians at (1E-4, 1E-6) passed the test and
            // then threw building its frame.
            if (!corner.GetVectorTo(incoming.StartPoint).TryGetNormal(out GeoVector3 toPrev, tolerance) ||
                !corner.GetVectorTo(outgoing.EndPoint).TryGetNormal(out GeoVector3 toNext, tolerance) ||
                !toNext.CrossProduct(toPrev).TryGetNormal(out GeoVector3 normal, tolerance))
            {
                return false;
            }

            var frame = new GeoCoordinateSystem3(corner, toNext, normal.CrossProduct(toNext));

            var flatIn = new GeoEdge2(Flat(frame, incoming.StartPoint), Flat(frame, corner));
            var flatOut = new GeoEdge2(Flat(frame, corner), Flat(frame, outgoing.EndPoint));

            if (!ArcFillet2.TryFillet(flatIn, flatOut, radius, out GeoArc2 arc, out GeoEdge2 shorter1, out GeoEdge2 shorter2, tolerance))
            {
                return false;
            }

            GeoPoint3 first = Lift(frame, shorter1.EndPoint);
            GeoPoint3 second = Lift(frame, shorter2.StartPoint);

            // An arc whose two ends the tolerance cannot tell apart has no chord to bulge along, and rounds
            // nothing: the bend from (10, 0, 0) on to (20, 0.7, 0) at a radius of 1E-4 touches its legs 3.5E-6
            // either side of the corner. It is left as it is.
            if (!first.GetVectorTo(second).TryGetNormal(out _, tolerance))
            {
                return false;
            }

            rounded = new Rounded(
                new GeoEdge3(incoming.StartPoint, first),
                new GeoEdge3(first, second, arc.Bulge, frame.ZAxis, tolerance),
                new GeoEdge3(second, outgoing.EndPoint));

            return true;
        }

        /// <summary>
        /// Gets an edge with the same shape as another but new ends, which for a straight piece is the piece
        /// between them and for an arc is the same arc cut back.
        /// </summary>
        private static GeoEdge3 Remade(GeoEdge3 edge, GeoPoint3 start, GeoPoint3 end)
        {
            if (!edge.IsArc)
            {
                return new GeoEdge3(start, end);
            }

            double from = edge.GetParameterAtPoint(start);
            double to = edge.GetParameterAtPoint(end);
            double sweep = 4.0 * Math.Atan(edge.Bulge);

            return new GeoEdge3(start, end, Math.Tan(sweep * (to - from) * 0.25), edge.Normal);
        }

        /// <summary>
        /// Gets a point of the corner plane in that plane, where the third coordinate is nought.
        /// </summary>
        private static GeoPoint2 Flat(GeoCoordinateSystem3 frame, GeoPoint3 point)
        {
            GeoPoint3 local = frame.ToLocal(point);

            return new GeoPoint2(local.X, local.Y);
        }

        /// <summary>
        /// Puts a point of the corner plane back into space.
        /// </summary>
        private static GeoPoint3 Lift(GeoCoordinateSystem3 frame, GeoPoint2 point)
            => frame.ToGlobal(new GeoPoint3(point.X, point.Y, 0.0));

        /// <summary>
        /// Gets one radius for every corner of a run of edges.
        /// </summary>
        private static double?[] Everywhere(double radius, int edgeCount)
        {
            var radii = new double?[edgeCount];

            for (int i = 0; i < edgeCount; i++)
            {
                radii[i] = radius;
            }

            return radii;
        }

        /// <summary>
        /// Gets the radius wanted at each corner, from a list laid out by vertex.
        /// </summary>
        private static double?[] ByCorner(IReadOnlyList<double> radii, int edgeCount, int vertexCount)
        {
            var wanted = new double?[edgeCount];

            // The corner between edge c and edge c + 1 stands at vertex c + 1.
            for (int c = 0; c < edgeCount - 1; c++)
            {
                int vertex = c + 1;

                if (vertex >= vertexCount || vertex >= radii.Count)
                {
                    continue;
                }

                if (radii[vertex] > 0.0)
                {
                    wanted[c] = radii[vertex];
                }
            }

            return wanted;
        }

        /// <summary>
        /// Refuses a radius that is not a positive number.
        /// </summary>
        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "A radius has to be a positive number.");
            }
        }

        /// <summary>
        /// Refuses a list of radii that holds something no corner could take.
        /// </summary>
        private static void RequireRadii(IReadOnlyList<double> radii)
        {
            if (radii == null)
            {
                throw new ArgumentNullException(nameof(radii));
            }

            foreach (double radius in radii)
            {
                if (double.IsNaN(radius) || radius < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(radii), "A radius cannot be negative, and nought means the corner is left alone.");
                }
            }
        }

        /// <summary>
        /// What rounding one corner leaves: the shortened leg going in, the arc, and the shortened leg coming out.
        /// </summary>
        private struct Rounded
        {
            internal Rounded(GeoEdge3 before, GeoEdge3 arc, GeoEdge3 after)
            {
                Before = before;
                Arc = arc;
                After = after;
            }

            internal GeoEdge3 Before { get; }

            internal GeoEdge3 Arc { get; }

            internal GeoEdge3 After { get; }
        }
    }
}
