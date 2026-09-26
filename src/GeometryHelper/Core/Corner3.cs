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
        /// <returns>The chain with its corners rounded; a corner with too little edge, or none to turn, is left as it was.</returns>
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

            var edges = new List<GeoEdge3>();

            for (int e = 0; e < chain.EdgeCount; e++)
            {
                if (e == index - 1)
                {
                    Keep(edges, rounded.Before, tolerance);
                    edges.Add(rounded.Arc);
                    Keep(edges, rounded.After, tolerance);
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
                takenAtEnd[c] = edges[c].Length - rounded.Before.Length;
                takenAtStart[c + 1] = edges[c + 1].Length - rounded.After.Length;
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

            var result = new List<GeoEdge3>(count * 2);

            for (int e = 0; e < count; e++)
            {
                GeoEdge3 edge = edges[e];
                bool cutBefore = e > 0 && doCut[e - 1];

                GeoPoint3 start = cutBefore ? cutTo[e - 1] : edge.StartPoint;
                GeoPoint3 end = doCut[e] ? cutFrom[e] : edge.EndPoint;

                Keep(result, Remade(edge, start, end), tolerance);

                if (doCut[e])
                {
                    result.Add(arcAt[e]);
                }
            }

            return new GeoPolylineArc3(result);
        }

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
            GeoVector3 toPrev = corner.GetVectorTo(incoming.StartPoint);
            GeoVector3 toNext = corner.GetVectorTo(outgoing.EndPoint);

            // Two legs running straight on have no corner to round, and name no plane either.
            if (!toNext.CrossProduct(toPrev).TryGetNormal(out _, tolerance))
            {
                return false;
            }

            var frame = new GeoCoordinateSystem3(corner, toNext, toPrev);

            var flatIn = new GeoEdge2(Flat(frame, incoming.StartPoint), Flat(frame, corner));
            var flatOut = new GeoEdge2(Flat(frame, corner), Flat(frame, outgoing.EndPoint));

            if (!ArcFillet2.TryFillet(flatIn, flatOut, radius, out GeoArc2 arc, out GeoEdge2 shorter1, out GeoEdge2 shorter2, tolerance))
            {
                return false;
            }

            GeoPoint3 first = Lift(frame, shorter1.EndPoint);
            GeoPoint3 second = Lift(frame, shorter2.StartPoint);

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
        /// Adds an edge unless it has shrunk to nothing, which is not geometry and would cost the arc after
        /// it its bulge.
        /// </summary>
        private static void Keep(List<GeoEdge3> edges, GeoEdge3 edge, Tolerance tolerance)
        {
            if (edge.IsArc || !edge.StartPoint.IsEqualTo(edge.EndPoint, tolerance))
            {
                edges.Add(edge);
            }
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
