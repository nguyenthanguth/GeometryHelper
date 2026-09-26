using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting the corners of a chain in space, and rounding the corners of a loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rounding was offered in space and cutting was not, the opposite way round from the plane. A chamfer needs
    /// no plane at all: it moves back along one leg and forward along the other, so the two points it leaves are
    /// on the legs themselves and the answer is exact wherever the chain goes. That is why this sits beside
    /// <see cref="Fillet(GeoPolyline3, double)"/> rather than inside the per-corner frame the fillet needs.
    /// </para>
    /// <para>
    /// The rules are the plane's, rule for rule: a corner that does not turn has nothing to cut off, a corner is
    /// left alone when a leg is shorter than the distance asked of it, and an edge can give away no more than it
    /// is long counting both of its ends — where it cannot, the corner taking more of it gives way, and on a tie
    /// the earlier one, so the answer does not depend on where the walk began.
    /// </para>
    /// <para>
    /// Only a corner between two <b>straight</b> legs is cut, the same rule the fillet keeps. A leg that curves
    /// leaves at a tangent, so cutting the corner off it would move the cut onto the arc and change its radius.
    /// </para>
    /// </remarks>
    public static partial class Corner3
    {
        #region Cutting the corners of a straight chain

        /// <summary>
        /// Cuts every corner of a straight chain back by the same distance.
        /// </summary>
        public static GeoPolyline3 Chamfer(GeoPolyline3 polyline, double distance) => Chamfer(polyline, distance, distance, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a straight chain back by the same distance, within a tolerance.
        /// </summary>
        public static GeoPolyline3 Chamfer(GeoPolyline3 polyline, double distance, Tolerance tolerance) => Chamfer(polyline, distance, distance, tolerance);

        /// <summary>
        /// Cuts every corner of a straight chain back, by one distance along the way in and another along the way out.
        /// </summary>
        public static GeoPolyline3 Chamfer(GeoPolyline3 polyline, double distance1, double distance2)
            => Chamfer(polyline, distance1, distance2, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a straight chain back, by one distance along the way in and another along the way
        /// out, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="distance1">How far back along the leg arriving at each corner to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with its corners cut; a corner with too little leg to give is left alone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolyline3 Chamfer(GeoPolyline3 polyline, double distance1, double distance2, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            List<GeoPoint3> vertices = BuildCut(polyline.Vertices, false, distance1, distance2, tolerance, "chain");

            return vertices == null ? polyline.Clone() : new GeoPolyline3(vertices);
        }

        /// <summary>
        /// Cuts one corner of a straight chain back.
        /// </summary>
        public static bool TryChamferAt(GeoPolyline3 polyline, int index, double distance1, double distance2, out GeoPolyline3 result)
            => TryChamferAt(polyline, index, distance1, distance2, out result, Tolerance.Global);

        /// <summary>
        /// Cuts one corner of a straight chain back, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="index">Which vertex to cut; the two ends of a chain are not corners.</param>
        /// <param name="distance1">How far back along the leg arriving there to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="result">The cut chain, or the chain unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was cut; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain, or either distance is not positive.</exception>
        public static bool TryChamferAt(GeoPolyline3 polyline, int index, double distance1, double distance2, out GeoPolyline3 result, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (index < 0 || index >= polyline.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            result = polyline;

            if (index == 0 || index == polyline.VertexCount - 1)
            {
                return false;
            }

            if (!TryCutVertex(polyline.Vertices, false, index, distance1, distance2, tolerance, out GeoPoint3 from, out GeoPoint3 to, out _))
            {
                return false;
            }

            result = new GeoPolyline3(Spliced(polyline.Vertices, index, from, to));
            return true;
        }

        #endregion

        #region Cutting the corners of a straight loop

        /// <summary>
        /// Cuts every corner of a straight loop back by the same distance.
        /// </summary>
        public static GeoPolygon3 Chamfer(GeoPolygon3 polygon, double distance) => Chamfer(polygon, distance, distance, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a straight loop back by the same distance, within a tolerance.
        /// </summary>
        public static GeoPolygon3 Chamfer(GeoPolygon3 polygon, double distance, Tolerance tolerance) => Chamfer(polygon, distance, distance, tolerance);

        /// <summary>
        /// Cuts every corner of a straight loop back, by one distance along the way in and another along the way out.
        /// </summary>
        public static GeoPolygon3 Chamfer(GeoPolygon3 polygon, double distance1, double distance2)
            => Chamfer(polygon, distance1, distance2, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a straight loop back, by one distance along the way in and another along the way
        /// out, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="distance1">How far back along the leg arriving at each corner to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with its corners cut; a corner with too little leg to give is left alone.</returns>
        /// <remarks>
        /// A loop is flat, so cutting its corners keeps it flat and the answer is a loop of the same sort. Every
        /// vertex of a loop is a corner, the last one included.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolygon3 Chamfer(GeoPolygon3 polygon, double distance1, double distance2, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            List<GeoPoint3> vertices = BuildCut(polygon.Vertices, true, distance1, distance2, tolerance, "loop");

            return vertices == null ? polygon.Clone() : new GeoPolygon3(vertices, tolerance);
        }

        /// <summary>
        /// Cuts one corner of a straight loop back.
        /// </summary>
        public static bool TryChamferAt(GeoPolygon3 polygon, int index, double distance1, double distance2, out GeoPolygon3 result)
            => TryChamferAt(polygon, index, distance1, distance2, out result, Tolerance.Global);

        /// <summary>
        /// Cuts one corner of a straight loop back, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="index">Which vertex to cut; every vertex of a loop is a corner.</param>
        /// <param name="distance1">How far back along the leg arriving there to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="result">The cut loop, or the loop unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was cut; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop, or either distance is not positive.</exception>
        public static bool TryChamferAt(GeoPolygon3 polygon, int index, double distance1, double distance2, out GeoPolygon3 result, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (index < 0 || index >= polygon.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            result = polygon;

            if (!TryCutVertex(polygon.Vertices, true, index, distance1, distance2, tolerance, out GeoPoint3 from, out GeoPoint3 to, out _))
            {
                return false;
            }

            result = new GeoPolygon3(Spliced(polygon.Vertices, index, from, to), tolerance);
            return true;
        }

        #endregion

        #region Cutting the corners of a curved chain

        /// <summary>
        /// Cuts every corner of a curved chain back by the same distance.
        /// </summary>
        public static GeoPolylineArc3 Chamfer(GeoPolylineArc3 chain, double distance) => Chamfer(chain, distance, distance, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a curved chain back by the same distance, within a tolerance.
        /// </summary>
        public static GeoPolylineArc3 Chamfer(GeoPolylineArc3 chain, double distance, Tolerance tolerance) => Chamfer(chain, distance, distance, tolerance);

        /// <summary>
        /// Cuts every corner of a curved chain back, by one distance along the way in and another along the way out.
        /// </summary>
        public static GeoPolylineArc3 Chamfer(GeoPolylineArc3 chain, double distance1, double distance2)
            => Chamfer(chain, distance1, distance2, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of a curved chain back, by one distance along the way in and another along the way
        /// out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance1">How far back along the leg arriving at each corner to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with the corners between straight legs cut; the rest are left alone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolylineArc3 Chamfer(GeoPolylineArc3 chain, double distance1, double distance2, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            List<GeoEdge3> edges = ArcChain3.EdgesOf(chain);
            var wanted = new bool[edges.Count];

            for (int c = 0; c < edges.Count - 1; c++)
            {
                wanted[c] = true;
            }

            return CutCorners(edges, wanted, distance1, distance2, tolerance);
        }

        /// <summary>
        /// Cuts one corner of a curved chain back.
        /// </summary>
        public static bool TryChamferAt(GeoPolylineArc3 chain, int index, double distance1, double distance2, out GeoPolylineArc3 result)
            => TryChamferAt(chain, index, distance1, distance2, out result, Tolerance.Global);

        /// <summary>
        /// Cuts one corner of a curved chain back, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="index">Which vertex to cut; the two ends of a chain are not corners.</param>
        /// <param name="distance1">How far back along the leg arriving there to start the cut.</param>
        /// <param name="distance2">How far along the leg leaving it to finish.</param>
        /// <param name="result">The cut chain, or the chain unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was cut; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain, or either distance is not positive.</exception>
        public static bool TryChamferAt(GeoPolylineArc3 chain, int index, double distance1, double distance2, out GeoPolylineArc3 result, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (index < 0 || index >= chain.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositiveDistance(distance1, nameof(distance1));
            RequirePositiveDistance(distance2, nameof(distance2));

            result = chain;

            if (index == 0 || index == chain.VertexCount - 1)
            {
                return false;
            }

            List<GeoEdge3> edges = ArcChain3.EdgesOf(chain);

            if (!TryCutEdges(edges[index - 1], edges[index], distance1, distance2, tolerance, out GeoPoint3 from, out GeoPoint3 to, out _))
            {
                return false;
            }

            var wanted = new bool[edges.Count];
            wanted[index - 1] = true;

            result = CutCorners(edges, wanted, distance1, distance2, tolerance);
            return true;
        }

        #endregion

        #region Rounding a straight loop

        /// <summary>
        /// Rounds every corner of a straight loop by the same radius.
        /// </summary>
        public static GeoPolygonArc3 Fillet(GeoPolygon3 polygon, double radius) => Fillet(polygon, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of a straight loop by the same radius, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="radius">The radius to round every corner by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with its corners rounded; a corner with too little edge to give is left alone.</returns>
        /// <remarks>
        /// A rounded corner is an arc, so a straight loop cannot hold the answer and the return type is
        /// <see cref="GeoPolygonArc3"/>. A loop is flat, so this is the coplanar lift and not the per-corner
        /// frame an open chain needs.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc3 Fillet(GeoPolygon3 polygon, double radius, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return new GeoPolygonArc3(polygon).Fillet(radius, tolerance);
        }

        /// <summary>
        /// Rounds the corners of a straight loop, one radius each.
        /// </summary>
        public static GeoPolygonArc3 Fillet(GeoPolygon3 polygon, IReadOnlyList<double> radii) => Fillet(polygon, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a straight loop, one radius each, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="radii">A radius per corner; nought leaves that corner square.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with the corners the radii asked for rounded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the list of radii is null.</exception>
        public static GeoPolygonArc3 Fillet(GeoPolygon3 polygon, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return new GeoPolygonArc3(polygon).Fillet(radii, tolerance);
        }

        /// <summary>
        /// Rounds one corner of a straight loop.
        /// </summary>
        public static bool TryFilletAt(GeoPolygon3 polygon, int index, double radius, out GeoPolygonArc3 result)
            => TryFilletAt(polygon, index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one corner of a straight loop, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="index">Which vertex to round; every vertex of a loop is a corner.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="result">The rounded loop, or the loop unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was rounded; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryFilletAt(GeoPolygon3 polygon, int index, double radius, out GeoPolygonArc3 result, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return new GeoPolygonArc3(polygon).TryFilletAt(index, radius, out result, tolerance);
        }

        #endregion

        #region Working the cut out

        /// <summary>
        /// Cuts the corners a walk asks for, giving way where an edge is too short to serve both of its ends.
        /// </summary>
        private static GeoPolylineArc3 CutCorners(
            List<GeoEdge3> edges,
            bool[] wanted,
            double distance1,
            double distance2,
            Tolerance tolerance)
        {
            int count = edges.Count;

            var doCut = new bool[count];
            var cutFrom = new GeoPoint3[count];
            var cutTo = new GeoPoint3[count];
            var takenAtStart = new double[count];
            var takenAtEnd = new double[count];

            int curved = 0;
            int straight = 0;
            int tooShort = 0;

            for (int c = 0; c < count - 1; c++)
            {
                if (!wanted[c])
                {
                    continue;
                }

                if (edges[c].IsArc || edges[c + 1].IsArc)
                {
                    curved++;
                    continue;
                }

                if (!TryCutEdges(edges[c], edges[c + 1], distance1, distance2, tolerance, out GeoPoint3 from, out GeoPoint3 to, out bool wasStraight))
                {
                    if (wasStraight)
                    {
                        straight++;
                    }
                    else
                    {
                        tooShort++;
                    }

                    continue;
                }

                doCut[c] = true;
                cutFrom[c] = from;
                cutTo[c] = to;
                takenAtEnd[c] = distance1;
                takenAtStart[c + 1] = distance2;
            }

            tooShort += GiveWayOnSharedEdges(edges, doCut, takenAtStart, takenAtEnd, tolerance);

            if (curved + straight + tooShort > 0)
            {
                GeometryHelperLog.Debug(
                    $"Chamfer left {curved + straight + tooShort} corner(s) in space alone: " +
                    $"{tooShort} had too little leg to cut, {straight} were straight, {curved} had a leg that curves.");
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
                    result.Add(new GeoEdge3(cutFrom[e], cutTo[e]));
                }
            }

            return new GeoPolylineArc3(result);
        }

        /// <summary>
        /// Drops the cuts that would take more of an edge than the edge is long, counting both of its ends.
        /// </summary>
        private static int GiveWayOnSharedEdges(
            List<GeoEdge3> edges,
            bool[] doCut,
            double[] takenAtStart,
            double[] takenAtEnd,
            Tolerance tolerance)
        {
            int count = edges.Count;
            int dropped = 0;
            bool again = true;

            while (again)
            {
                again = false;

                for (int e = 0; e < count; e++)
                {
                    // Exactly enough is enough: the dust left by measuring the edge must not decide otherwise.
                    if (takenAtStart[e] + takenAtEnd[e] <= edges[e].Length + tolerance.EqualPoint)
                    {
                        continue;
                    }

                    int atStart = e - 1;
                    int atEnd = e;

                    // Drop whichever of the two takes more of this edge; on a tie the earlier one, so the
                    // answer does not depend on where the walk began.
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
                    dropped++;
                    again = true;
                }
            }

            return dropped;
        }

        /// <summary>
        /// Cuts the corners of a walk over vertices, for the chains that hold no arcs.
        /// </summary>
        /// <returns>The vertices of the cut chain, or null when no corner was cut at all.</returns>
        private static List<GeoPoint3> BuildCut(
            IReadOnlyList<GeoPoint3> vertices,
            bool closed,
            double distance1,
            double distance2,
            Tolerance tolerance,
            string what)
        {
            int count = vertices.Count;
            var cut = new bool[count];
            var from = new GeoPoint3[count];
            var to = new GeoPoint3[count];

            int first = closed ? 0 : 1;
            int last = closed ? count - 1 : count - 2;
            int straight = 0;
            int tooShort = 0;

            for (int i = first; i <= last; i++)
            {
                if (TryCutVertex(vertices, closed, i, distance1, distance2, tolerance, out from[i], out to[i], out bool wasStraight))
                {
                    cut[i] = true;
                }
                else if (wasStraight)
                {
                    straight++;
                }
                else
                {
                    tooShort++;
                }
            }

            tooShort += GiveWayOnSharedLegs(vertices, closed, cut, distance1, distance2, tolerance);

            int cutCount = 0;

            foreach (bool one in cut)
            {
                if (one)
                {
                    cutCount++;
                }
            }

            if (straight + tooShort > 0)
            {
                GeometryHelperLog.Debug(
                    $"Chamfer left {straight + tooShort} corner(s) of the {what} in space alone: " +
                    $"{tooShort} had too little leg to cut, {straight} were straight.");
            }

            if (cutCount == 0)
            {
                return null;
            }

            var result = new List<GeoPoint3>(count + cutCount);

            for (int i = 0; i < count; i++)
            {
                if (cut[i])
                {
                    result.Add(from[i]);
                    result.Add(to[i]);
                }
                else
                {
                    result.Add(vertices[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Drops the cuts that would take more of a leg than the leg is long, counting both of its ends.
        /// </summary>
        private static int GiveWayOnSharedLegs(
            IReadOnlyList<GeoPoint3> vertices,
            bool closed,
            bool[] cut,
            double distance1,
            double distance2,
            Tolerance tolerance)
        {
            int count = vertices.Count;
            int legs = closed ? count : count - 1;
            int dropped = 0;
            bool again = true;

            while (again)
            {
                again = false;

                for (int e = 0; e < legs; e++)
                {
                    int start = e;
                    int end = (e + 1) % count;

                    // The corner at the start of this leg takes distance2 from it; the one at its end takes distance1.
                    double taken = (cut[start] ? distance2 : 0.0) + (cut[end] ? distance1 : 0.0);

                    if (taken <= vertices[start].DistanceTo(vertices[end]) + tolerance.EqualPoint)
                    {
                        continue;
                    }

                    int victim = !cut[start] ? end
                               : !cut[end] ? start
                               : distance2 >= distance1 ? start
                               : end;

                    cut[victim] = false;
                    dropped++;
                    again = true;
                }
            }

            return dropped;
        }

        /// <summary>
        /// Works out where a chamfer cuts the corner at one vertex of a walk.
        /// </summary>
        private static bool TryCutVertex(
            IReadOnlyList<GeoPoint3> vertices,
            bool closed,
            int index,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint3 from,
            out GeoPoint3 to,
            out bool straight)
        {
            int count = vertices.Count;
            from = default(GeoPoint3);
            to = default(GeoPoint3);
            straight = false;

            if (!closed && (index == 0 || index == count - 1))
            {
                return false;
            }

            GeoPoint3 previous = vertices[(index - 1 + count) % count];
            GeoPoint3 corner = vertices[index];
            GeoPoint3 next = vertices[(index + 1) % count];

            return TryCutAt(previous, corner, next, distance1, distance2, tolerance, out from, out to, out straight);
        }

        /// <summary>
        /// Works out where a chamfer cuts the corner between two edges that meet, refusing a leg that curves.
        /// </summary>
        private static bool TryCutEdges(
            GeoEdge3 incoming,
            GeoEdge3 outgoing,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint3 from,
            out GeoPoint3 to,
            out bool straight)
        {
            from = default(GeoPoint3);
            to = default(GeoPoint3);
            straight = false;

            if (incoming.IsArc || outgoing.IsArc)
            {
                return false;
            }

            return TryCutAt(incoming.StartPoint, incoming.EndPoint, outgoing.EndPoint, distance1, distance2, tolerance, out from, out to, out straight);
        }

        /// <summary>
        /// Moves back along one leg and forward along the other. No plane is needed: both points land on the
        /// legs themselves, so the cut is exact wherever the chain goes.
        /// </summary>
        private static bool TryCutAt(
            GeoPoint3 previous,
            GeoPoint3 corner,
            GeoPoint3 next,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint3 from,
            out GeoPoint3 to,
            out bool straight)
        {
            from = default(GeoPoint3);
            to = default(GeoPoint3);
            straight = false;

            GeoVector3 back = corner.GetVectorTo(previous);
            GeoVector3 forward = corner.GetVectorTo(next);

            double backLength = back.Length;
            double forwardLength = forward.Length;

            if (backLength <= tolerance.EqualPoint || forwardLength <= tolerance.EqualPoint)
            {
                return false;
            }

            // A corner that does not turn has nothing to cut off: the two points would land on one line and the
            // cut would be an edge of zero length.
            double sine = back.CrossProduct(forward).Length / (backLength * forwardLength);

            if (sine <= Math.Sin(tolerance.EqualAngleRad))
            {
                straight = true;
                return false;
            }

            if (distance1 > backLength + tolerance.EqualPoint || distance2 > forwardLength + tolerance.EqualPoint)
            {
                return false;
            }

            from = corner.Add(back.Multiply(distance1 / backLength));
            to = corner.Add(forward.Multiply(distance2 / forwardLength));
            return true;
        }

        /// <summary>
        /// The vertices of a walk with one of them replaced by the two ends of a cut.
        /// </summary>
        private static List<GeoPoint3> Spliced(IReadOnlyList<GeoPoint3> vertices, int index, GeoPoint3 from, GeoPoint3 to)
        {
            var result = new List<GeoPoint3>(vertices.Count + 1);

            for (int i = 0; i < vertices.Count; i++)
            {
                if (i == index)
                {
                    result.Add(from);
                    result.Add(to);
                }
                else
                {
                    result.Add(vertices[i]);
                }
            }

            return result;
        }

        private static void RequirePositiveDistance(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "A chamfer distance must be a positive number.");
            }
        }

        #endregion
    }
}
