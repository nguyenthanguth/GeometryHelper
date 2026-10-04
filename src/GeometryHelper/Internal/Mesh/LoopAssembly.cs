using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Turns a loose set of edges and loops into faces.
    /// <para>
    /// Two different jobs end in the same place. Cutting a body leaves a rim of edges that has to be closed
    /// into the surface capping each half; merging the coplanar faces of a body leaves the outlines of
    /// several faces that have to be closed into one. Both need the edge pairs running the same stretch in
    /// opposite directions removed, both need the survivors chained into loops, and both need those loops
    /// nested so that one inside another becomes a hole. That shared middle lives here rather than being
    /// written twice.
    /// </para>
    /// </summary>
    internal static class LoopAssembly
    {
        /// <summary>
        /// Walks every ring of a face in the direction the material lies to one consistent side: the outer
        /// boundary as given, each hole reversed.
        /// </summary>
        /// <remarks>
        /// This library stores a hole wound the same way as the boundary that carries it, which is what
        /// makes area and volume come out by plain subtraction. Walking the edge of the material is the
        /// other convention: there a hole runs the opposite way round, so that the material stays on the
        /// same hand throughout. Anything that traces the boundary — cutting it, or reading the edges the
        /// cut left behind — has to use this order, or the trace will turn the wrong way when it reaches a
        /// rim and lose the piece it was walking.
        /// </remarks>
        internal static IEnumerable<IReadOnlyList<GeoPoint3>> EnumerateMaterialRings(GeoFace3 face)
        {
            yield return face.Boundary.Vertices;

            foreach (GeoPolygon3 hole in face.Holes)
            {
                List<GeoPoint3> reversed = new List<GeoPoint3>(hole.Vertices);
                reversed.Reverse();
                yield return reversed;
            }
        }
        
        /// <summary>
        /// How many point tolerances apart the two ends of a gap in a rim may stand to be joined: the crossings of two copies
        /// of one edge a point tolerance apart, met at a slant of fifteen degrees.
        /// </summary>
        internal const double Bridge = 4.0;

        /// <summary>
        /// Joins the ends a rim leaves open where two faces crossed the edge they share on copies of it a hair apart: each
        /// end left with no edge after it to the start left with none before it that is nearest, and the other way round.
        /// </summary>
        /// <param name="edges">The edges of the rim.</param>
        /// <param name="tolerance">The tolerance deciding which ends are one point.</param>
        /// <param name="bridged">The edges, and an edge across each gap; null when the method returns false.</param>
        /// <returns>
        /// false when no end is open, or an open end has no open start that is its nearest and it the start's, within
        /// <see cref="Bridge"/> point tolerances.
        /// </returns>
        /// <remarks>
        /// Two faces of a body can share an edge within the tolerance whose ends are not one point, as the booleans leave
        /// corners a few thousandths apart, and the body is closed across them. Each face crosses the plane on its own copy,
        /// and where the plane meets the edge at a slant the crossings stand further apart than the copies' ends do: copies
        /// whose top corners stood 0.0045 apart in height above a plane meeting them at 22 degrees crossed it 0.0112 apart,
        /// and the rim did not close. A short edge between the two crossings closes it, and lies within the tolerance of both
        /// copies, as the stretch of the edge between the crossings does. Putting both faces' crossings on one point instead
        /// took the two sides of a tube's wall thinner than the tolerance for one, and its section for none.
        /// </remarks>
        internal static bool TryBridgeGaps(List<GeoLine3> edges, Tolerance tolerance, out List<GeoLine3> bridged)
        {
            bridged = null;

            var welder = new VertexWelder(tolerance);
            var points = new Dictionary<int, GeoPoint3>();
            var balance = new Dictionary<int, int>();

            void Count(GeoPoint3 point, int step)
            {
                int index = welder.GetIndex(point);

                if (!points.ContainsKey(index))
                {
                    points.Add(index, point);
                }

                balance[index] = (balance.TryGetValue(index, out int seen) ? seen : 0) + step;
            }

            foreach (GeoLine3 edge in edges)
            {
                Count(edge.StartPoint, 1);
                Count(edge.EndPoint, -1);
            }

            // An end has one edge more arriving than leaving, a start one more leaving than arriving.
            var ends = new List<GeoPoint3>();
            var starts = new List<GeoPoint3>();

            foreach (KeyValuePair<int, int> vertex in balance)
            {
                if (vertex.Value == -1)
                {
                    ends.Add(points[vertex.Key]);
                }
                else if (vertex.Value == 1)
                {
                    starts.Add(points[vertex.Key]);
                }
                else if (vertex.Value != 0)
                {
                    return false;
                }
            }

            if (ends.Count == 0 || ends.Count != starts.Count)
            {
                return false;
            }

            double reach = Bridge * tolerance.EqualPoint;
            var joined = new List<GeoLine3>(edges);

            foreach (GeoPoint3 end in ends)
            {
                GeoPoint3 start = Nearest(starts, end);

                // Each the other's nearest, so that no gap is closed across another.
                if (!(end.DistanceTo(start) <= reach) || !Nearest(ends, start).Equals(end))
                {
                    return false;
                }

                joined.Add(new GeoLine3(end, start));
            }

            bridged = joined;
            return true;
        }

        /// <summary>
        /// Closes the slivers a half of a cut is left open by where its rim was bridged: each strip between two copies of an
        /// edge that stood further apart at the cut than the tolerance, closed by a face across it.
        /// </summary>
        /// <param name="faces">The faces of the half, its caps included.</param>
        /// <param name="tolerance">The tolerance deciding which corners are one point.</param>
        /// <param name="pieces">The tolerance the closing faces are built within; see <see cref="ForPieces"/>.</param>
        /// <param name="slivers">The faces closing the slivers, none where nothing is open; null when the method returns false.</param>
        /// <returns>
        /// false when the edges left open do not chain into loops one way only, or a loop is no sliver: wider across than
        /// <see cref="Bridge"/> point tolerances, or no face.
        /// </returns>
        /// <remarks>
        /// Two faces sharing an edge on copies a hair apart are closed across it, the corners at each end one point within
        /// the tolerance. A cut takes each copy as it finds it, and where a face's two crossings stand within the tolerance
        /// of each other they are one corner, the one on the far copy as often as not: the crossings were left 0.0119
        /// apart, the rim was bridged, and the strip between the copies, from the cut up to where they meet, was a sliver no
        /// face covered, 102.8 long. It is a piece of a face of the body all the same, and a face across it closes the half,
        /// where one point for both copies would take the two sides of a wall thinner than the tolerance for one. A loop of
        /// no area is an edge run beside two that make it up, which the surface closes as it is, and is passed over.
        /// </remarks>
        internal static bool TryCloseSlivers(List<GeoFace3> faces, Tolerance tolerance, Tolerance pieces, out List<GeoFace3> slivers)
        {
            slivers = null;

            var welder = new VertexWelder(tolerance);
            var points = new Dictionary<int, GeoPoint3>();
            var runs = new Dictionary<long, int>();

            int Index(GeoPoint3 point)
            {
                int index = welder.GetIndex(point);

                if (!points.ContainsKey(index))
                {
                    points.Add(index, point);
                }

                return index;
            }

            foreach (GeoFace3 face in faces)
            {
                foreach (IReadOnlyList<GeoPoint3> ring in EnumerateMaterialRings(face))
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        int from = Index(ring[i]);
                        int to = Index(ring[(i + 1) % ring.Count]);

                        if (from != to)
                        {
                            long run = Run(from, to);
                            runs[run] = (runs.TryGetValue(run, out int seen) ? seen : 0) + 1;
                        }
                    }
                }
            }

            // In a closed surface every edge is run as often one way as the other. One run more often one way is open, and
            // the face closing it runs it the other way: each such edge, from its end to its start, by where it leaves.
            var leaving = new Dictionary<int, List<int>>();

            foreach (KeyValuePair<long, int> run in runs)
            {
                int from = (int)(run.Key >> 32);
                int to = (int)(run.Key & 0xFFFFFFFF);

                for (int extra = run.Value - (runs.TryGetValue(Run(to, from), out int back) ? back : 0); extra > 0; extra--)
                {
                    if (!leaving.TryGetValue(to, out List<int> ends))
                    {
                        ends = new List<int>();
                        leaving.Add(to, ends);
                    }

                    ends.Add(from);
                }
            }

            var closing = new List<GeoFace3>();

            while (leaving.Count > 0)
            {
                int start = 0;

                foreach (int key in leaving.Keys)
                {
                    start = key;
                    break;
                }

                var loop = new List<GeoPoint3> { points[start] };
                int at = start;

                while (true)
                {
                    // Two ways on from one corner is no strip between two copies of an edge.
                    if (!leaving.TryGetValue(at, out List<int> ends) || ends.Count != 1)
                    {
                        return false;
                    }

                    int next = ends[0];
                    leaving.Remove(at);

                    if (next == start)
                    {
                        break;
                    }

                    loop.Add(points[next]);
                    at = next;
                }

                if (loop.Count < 3)
                {
                    return false;
                }

                // The loop's area against its longest edge: how wide across it is.
                GeoVector3 area = new GeoVector3(0.0, 0.0, 0.0);
                double longest = 0.0;

                for (int i = 0; i < loop.Count; i++)
                {
                    GeoPoint3 a = loop[i];
                    GeoPoint3 b = loop[(i + 1) % loop.Count];
                    area = area.Add(loop[0].GetVectorTo(a).CrossProduct(loop[0].GetVectorTo(b)));
                    longest = Math.Max(longest, a.DistanceTo(b));
                }

                double size = 0.5 * area.Length;

                if (size <= tolerance.EqualPoint * tolerance.EqualPoint)
                {
                    continue;
                }

                if (!(2.0 * size <= Bridge * tolerance.EqualPoint * longest))
                {
                    return false;
                }

                GeoFace3[] across = Loops3.ToFaces(loop, null, pieces);

                if (across.Length == 0)
                {
                    return false;
                }

                closing.AddRange(across);
            }

            slivers = closing;
            return true;
        }

        private static long Run(int from, int to) => ((long)from << 32) | (uint)to;

        private static GeoPoint3 Nearest(List<GeoPoint3> points, GeoPoint3 to)
        {
            GeoPoint3 nearest = points[0];

            foreach (GeoPoint3 point in points)
            {
                if (point.DistanceTo(to) < nearest.DistanceTo(to))
                {
                    nearest = point;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Chains a set of directed edges into closed loops.
        /// </summary>
        /// <param name="edges">The edges; each is used once and each must have a neighbour at both ends.</param>
        /// <param name="normal">The normal of the plane the edges lie in, which fixes what a left turn is.</param>
        /// <param name="tolerance">The tolerance deciding whether two endpoints are the same point.</param>
        /// <param name="loops">The closed loops the edges form.</param>
        /// <returns>false when any chain came back open, meaning the edges do not close.</returns>
        /// <remarks>
        /// An open chain is a failure rather than a partial answer: a rim that does not close cannot be
        /// capped and a set of outlines that does not close is not a face, so guessing at either would
        /// produce geometry that looks right and is not.
        /// <para>
        /// More than two edges can meet at one point — two outlines touching at a corner, a vertex four
        /// edges arrive at — and there the walk has a choice to make. Taking whichever edge comes first
        /// welds the two outlines into a single figure of eight whose lobes run opposite ways, so its area
        /// is their difference rather than their sum and the surface built from it reports a size it does
        /// not have. The choice is settled by turning: of the edges leaving the vertex, the walk takes the
        /// one reached first by rotating from the direction it arrived on, counter-clockwise about the
        /// plane normal. Since every edge is directed so that the material lies to its left, that keeps
        /// the walk on the outline it is already following and leaves the other to be traced on its own.
        /// </para>
        /// </remarks>
        internal static bool TryChainLoops(List<GeoLine3> edges, GeoVector3 normal, Tolerance tolerance, out List<List<GeoPoint3>> loops)
        {
            loops = new List<List<GeoPoint3>>();

            int count = edges.Count;

            if (count == 0)
            {
                return false;
            }

            // Endpoints are matched through a welder so that two edges meeting at a corner agree on which
            // vertex it is, rather than being compared pair by pair.
            VertexWelder welder = new VertexWelder(tolerance);
            int[] from = new int[count];
            int[] to = new int[count];

            for (int i = 0; i < count; i++)
            {
                from[i] = welder.GetIndex(edges[i].StartPoint);
                to[i] = welder.GetIndex(edges[i].EndPoint);
            }

            Dictionary<int, List<int>> leaving = new Dictionary<int, List<int>>();

            for (int i = 0; i < count; i++)
            {
                if (!leaving.TryGetValue(from[i], out List<int> list))
                {
                    list = new List<int>();
                    leaving[from[i]] = list;
                }

                list.Add(i);
            }

            bool[] used = new bool[count];

            for (int seed = 0; seed < count; seed++)
            {
                if (used[seed])
                {
                    continue;
                }

                List<GeoPoint3> loop = new List<GeoPoint3>();
                int current = seed;
                int start = from[seed];
                used[seed] = true;

                while (true)
                {
                    loop.Add(edges[current].StartPoint);

                    int arrived = to[current];

                    if (arrived == start)
                    {
                        break;
                    }

                    int next = PickNextEdge(edges, leaving, used, current, arrived, normal);

                    if (next < 0)
                    {
                        // The walk ran out of edges before coming back, so these edges do not close.
                        return false;
                    }

                    used[next] = true;
                    current = next;
                }

                if (loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }

            return loops.Count > 0;
        }

        /// <summary>
        /// Chooses which edge the walk leaves a vertex on.
        /// </summary>
        /// <remarks>
        /// Of the edges still unused that leave the vertex, the one taken is the first met when rotating
        /// counter-clockwise about the plane normal from the direction the walk arrived on. An edge
        /// carrying straight on is therefore preferred to any turn, and a turn back the way the walk came
        /// is taken only when there is nothing else.
        /// </remarks>
        private static int PickNextEdge(List<GeoLine3> edges, Dictionary<int, List<int>> leaving, bool[] used, int current, int vertex, GeoVector3 normal)
        {
            if (!leaving.TryGetValue(vertex, out List<int> candidates))
            {
                return -1;
            }

            GeoVector3 arriving = edges[current].StartPoint.GetVectorTo(edges[current].EndPoint);

            int best = -1;
            double bestTurn = double.MaxValue;

            foreach (int candidate in candidates)
            {
                if (used[candidate])
                {
                    continue;
                }

                GeoVector3 departing = edges[candidate].StartPoint.GetVectorTo(edges[candidate].EndPoint);
                double turn = TurnAngle(arriving, departing, normal);

                if (turn < bestTurn)
                {
                    bestTurn = turn;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the angle turned counter-clockwise about a normal to get from one direction to another,
        /// in the range [0, 2*PI).
        /// </summary>
        /// <remarks>
        /// Atan2 of the cross and dot products is used rather than Acos, for the reason it is used
        /// everywhere else here: it is stable where the two directions are nearly equal or nearly
        /// opposite, which is exactly where the choice between two edges is decided. Neither argument
        /// needs normalizing, since both scale together with the lengths.
        /// </remarks>
        private static double TurnAngle(GeoVector3 arriving, GeoVector3 departing, GeoVector3 normal)
        {
            double cross = arriving.CrossProduct(departing).DotProduct(normal);
            double dot = arriving.DotProduct(departing);

            double turn = Math.Atan2(cross, dot);

            // Carrying straight on must read as no turn at all rather than as a full circle, and rounding
            // can put it a hair on either side of zero.
            if (turn < -1E-9)
            {
                return turn + 2.0 * Math.PI;
            }

            return turn < 0.0 ? 0.0 : turn;
        }

        /// <summary>
        /// Turns a set of closed loops into faces, working out which loops are holes inside which.
        /// </summary>
        /// <param name="loops">The loops, in any order; they must not cross one another.</param>
        /// <param name="orientation">The normal every resulting face should report.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Nesting is decided by containment rather than by winding, because the two sources of loops here
        /// disagree about winding: a hole carried through from the subject is wound the same way as its
        /// boundary, while a hole appearing in a section is wound the opposite way. Counting how many other
        /// loops enclose a loop settles it either way — an odd count makes it a hole, an even one makes it
        /// an outer boundary — and the winding is then set to match rather than trusted.
        /// </remarks>
        internal static List<GeoFace3> AssembleFaces(List<List<GeoPoint3>> loops, GeoVector3 orientation, Tolerance tolerance)
        {
            Assemble(loops, loop => TryBuildPolygon(loop, orientation, tolerance), tolerance, true, out List<GeoFace3> faces);
            return faces;
        }

        /// <summary>
        /// Turns closed loops lying in a plane into faces as <see cref="AssembleFaces"/> does, each read flat in the plane
        /// and facing along its normal.
        /// </summary>
        /// <param name="loops">The loops, every corner within the planar tolerance of the plane; they must not cross one another.</param>
        /// <param name="plane">The plane, its normal the way every face is to face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A polygon is measured from a corner of its own about the normal of its own corners, and a loop whose corners all
        /// lie within the tolerance of a plane can stand further than that from both: a cap with a finger a tenth of a
        /// millimetre wide, the corner at its tip 0.0084 off the cutting plane, had the normal of its corners turned 8E-5
        /// from the plane's, which put its far corners 0.058 off, and it was refused, leaving each half open. Read in the
        /// plane, a loop faces along the plane's normal from its corner nearest the plane, and the loops are nested and a
        /// face's holes held to its plane within twice the planar tolerance: a corner a hair one way, another a hair the
        /// other.
        /// </remarks>
        internal static List<GeoFace3> AssembleFacesIn(List<List<GeoPoint3>> loops, GeoPlane3 plane, Tolerance tolerance)
        {
            var flat = new Tolerance(tolerance.EqualPoint, tolerance.EqualVector, tolerance.EqualAngleRad, 2.0 * tolerance.EqualPlanar);
            Assemble(loops, loop => TryBuildPolygonIn(loop, plane, tolerance), flat, true, out List<GeoFace3> faces);
            return faces;
        }

        /// <summary>
        /// Nests closed loops into faces as <see cref="AssembleFaces"/> does, refusing loops that do not lie in one plane.
        /// </summary>
        /// <param name="loops">The loops.</param>
        /// <param name="orientation">The side the faces should face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="faces">The faces; null when the method returns false.</param>
        /// <returns>false where a hole stands further than the tolerance off the plane of the loop round it.</returns>
        internal static bool TryAssembleFaces(List<List<GeoPoint3>> loops, GeoVector3 orientation, Tolerance tolerance, out List<GeoFace3> faces)
            => Assemble(loops, loop => TryBuildPolygon(loop, orientation, tolerance), tolerance, false, out faces);

        /// <param name="loops">The loops.</param>
        /// <param name="build">The polygon of a loop, or null where it makes none.</param>
        /// <param name="tolerance">The tolerance the polygons are nested within and a face's holes held to its plane.</param>
        /// <param name="anyPlane">Whether a hole may stand off the plane of the loop round it.</param>
        /// <param name="faces">The faces; null when the method returns false.</param>
        private static bool Assemble(List<List<GeoPoint3>> loops, Func<List<GeoPoint3>, GeoPolygon3> build, Tolerance tolerance, bool anyPlane, out List<GeoFace3> faces)
        {
            faces = null;
            List<GeoPolygon3> polygons = new List<GeoPolygon3>();

            foreach (List<GeoPoint3> loop in loops)
            {
                GeoPolygon3 polygon = build(loop);

                if (polygon != null)
                {
                    polygons.Add(polygon);
                }
            }

            var assembled = new List<GeoFace3>();
            int[] depth = new int[polygons.Count];

            for (int i = 0; i < polygons.Count; i++)
            {
                for (int j = 0; j < polygons.Count; j++)
                {
                    if (i != j && IsLoopInside(polygons[i], polygons[j], tolerance))
                    {
                        depth[i]++;
                    }
                }
            }

            for (int i = 0; i < polygons.Count; i++)
            {
                if (depth[i] % 2 != 0)
                {
                    continue;
                }

                List<GeoPolygon3> holes = new List<GeoPolygon3>();

                for (int j = 0; j < polygons.Count; j++)
                {
                    // A hole belongs to the loop immediately outside it, which is the one exactly one level
                    // shallower that encloses it.
                    if (depth[j] == depth[i] + 1 && IsLoopInside(polygons[j], polygons[i], tolerance))
                    {
                        holes.Add(polygons[j]);
                    }
                }

                if (!anyPlane && holes.Count > 0 && !AllOn(polygons[i].GetPlane(), holes, tolerance))
                {
                    return false;
                }

                assembled.Add(new GeoFace3(polygons[i], holes, tolerance));
            }

            faces = assembled;
            return true;
        }

        private static bool AllOn(GeoPlane3 plane, List<GeoPolygon3> holes, Tolerance tolerance)
        {
            foreach (GeoPolygon3 hole in holes)
            {
                if (!plane.ContainsAll(hole.Vertices, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether one loop lies inside another. The two must not cross.
        /// </summary>
        /// <remarks>
        /// Loops that do not cross are either wholly inside or wholly outside one another, so one vertex
        /// settles it. Vertices sitting on the other loop say nothing, which is why the scan keeps looking
        /// until it finds one that does.
        /// <para>
        /// A hole can touch the rim of the loop round it at every corner: a hole meeting the boundary at two, cut
        /// through its third, leaves one in the piece on that side. Its sides still run through the material
        /// between, so the middle of a side settles it where no corner does. Read as outside, such a hole came
        /// back as a face of its own, and the piece covered it twice.
        /// </para>
        /// </remarks>
        internal static bool IsLoopInside(GeoPolygon3 inner, GeoPolygon3 outer, Tolerance tolerance)
        {
            foreach (GeoPoint3 vertex in inner.Vertices)
            {
                PointLocation location = Containment3.Locate(outer, vertex, tolerance);

                if (location == PointLocation.Inside)
                {
                    return true;
                }

                if (location == PointLocation.OutSide)
                {
                    return false;
                }
            }

            IReadOnlyList<GeoPoint3> corners = inner.Vertices;

            for (int i = 0; i < corners.Count; i++)
            {
                PointLocation location = Containment3.Locate(outer, corners[i].GetMiddlePoint(corners[(i + 1) % corners.Count]), tolerance);

                if (location == PointLocation.Inside)
                {
                    return true;
                }

                if (location == PointLocation.OutSide)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the tolerance a piece of an existing surface is built within: the one given, with an area as small
        /// as its point and planar tolerances allow.
        /// </summary>
        /// <remarks>
        /// A polygon refuses an area below the vector tolerance read as an area: 1e-5 by default, a triangle some
        /// five thousandths of a millimetre across, and 1e-2 up to 10.0.0, some fifteen hundredths. A plane passing a few thousandths from a corner of a body leaves a
        /// sliver that small on each face meeting at it, and the pieces of a surface rejoined can close a loop
        /// as thin. Refused, the surface has a hole there. A cut that loses one does not close and fails, leaving
        /// the body whole across the plane — two blocks sharing sixty were found to share nothing that way — and
        /// a merge that loses one leaves the body open. Such a piece has two corners further apart than the point
        /// tolerance, or they would be one, and a third further than the planar tolerance from the line through
        /// them, or it would be on it, so its area is at least half the two together; an eighth keeps every one
        /// and still refuses what rounding leaves of three points in a line.
        /// </remarks>
        internal static Tolerance ForPieces(Tolerance tolerance)
            => new Tolerance(
                tolerance.EqualPoint,
                Math.Min(tolerance.EqualVector, tolerance.EqualPoint * tolerance.EqualPlanar * 0.125),
                tolerance.EqualAngleRad,
                tolerance.EqualPlanar);

        /// <summary>
        /// Gets the tolerance the pieces of a face cut in two are built within: the one given, with three times its planar
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// A face a hair out of flat stands within the planar tolerance of its plane, and so does every piece of it, whose
        /// corners are the face's and points on its sides. But a polygon is measured from a corner of its own, about the
        /// normal of its own corners: from a corner a hair one way, a corner a hair the other stands off by twice the
        /// tolerance, and the normal of a piece lopsided about the face turns that a little more. Measured so, a piece of a
        /// face of a turned column was 0.0146 out of flat, it was refused, and the half it belonged to could not close. A
        /// piece can be no further out of flat than the face it is cut from.
        /// </remarks>
        internal static Tolerance ForFacePieces(Tolerance tolerance)
            => new Tolerance(tolerance.EqualPoint, tolerance.EqualVector, tolerance.EqualAngleRad, 3.0 * tolerance.EqualPlanar);

        /// <summary>
        /// Builds a polygon from a walked loop, matching the orientation asked for.
        /// </summary>
        /// <returns>null when the loop is too small or too thin to be a polygon.</returns>
        internal static GeoPolygon3 TryBuildPolygon(List<GeoPoint3> loop, GeoVector3 orientation, Tolerance tolerance)
        {
            try
            {
                GeoPolygon3 piece = new GeoPolygon3(loop, tolerance);

                // Walking a run can come out either way round depending on where it started, and a piece
                // that reports the opposite normal to its parent would break every downstream test.
                return piece.Normal.DotProduct(orientation) >= 0.0 ? piece : piece.Flip();
            }
            catch (ArgumentException)
            {
                // Slivers along the cut are the expected failure here: fewer than three distinct vertices,
                // or three collinear ones. Neither is a piece worth reporting.
                return null;
            }
        }

        /// <summary>
        /// Builds a polygon from a walked loop lying in a plane, read flat in it: facing along the plane's normal, from its
        /// corner nearest the plane.
        /// </summary>
        /// <returns>null when the loop is too small to be a polygon.</returns>
        private static GeoPolygon3 TryBuildPolygonIn(List<GeoPoint3> loop, GeoPlane3 plane, Tolerance tolerance)
        {
            // The corners a polygon keeps: none within the point tolerance of the one before it, nor the last of the first.
            var kept = new List<GeoPoint3>(loop.Count);

            foreach (GeoPoint3 point in loop)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(point, tolerance))
                {
                    kept.Add(point);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count < 3)
            {
                return null;
            }

            // The plane of a polygon passes through its first corner.
            int first = 0;

            for (int i = 1; i < kept.Count; i++)
            {
                if (Math.Abs(plane.SignedDistanceTo(kept[i])) < Math.Abs(plane.SignedDistanceTo(kept[first])))
                {
                    first = i;
                }
            }

            var corners = new GeoPoint3[kept.Count];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = kept[(first + i) % kept.Count];
            }

            // Its area along the plane's normal, refused below the least area a polygon encloses, as its constructor refuses it.
            double area = Newell.GetAreaVector(corners).DotProduct(plane.Normal);

            if (!(Math.Abs(area) > tolerance.EqualVector))
            {
                return null;
            }

            if (area < 0.0)
            {
                // Run the other way round from the same first corner.
                Array.Reverse(corners, 1, corners.Length - 1);
                area = -area;
            }

            return GeoPolygon3.FromValidated(corners, plane.Normal, area);
        }

        /// <summary>
        /// Splits every edge wherever another edge ends partway along it, so that two faces meeting along
        /// a stretch describe it with the same edges.
        /// </summary>
        /// <remarks>
        /// Faces that share a stretch of boundary need not divide it the same way. One face may have been
        /// cut in two along the way while its neighbour was left whole, and then one long edge faces two
        /// short ones across the same line — a T-junction. Cancelling looks for a pair running the same
        /// stretch in opposite directions and finds none, so all three survive and are read as boundary.
        /// <para>
        /// Cutting every edge at the ends of the others first removes the mismatch: after it, an interior
        /// stretch is described identically from both sides and cancels as it should. Without it the
        /// surviving edges chain into loops that look plausible and enclose the wrong area, which is how a
        /// merged body ends up reporting a volume it does not have.
        /// </para>
        /// </remarks>
        private static void SplitAtEdgeEnds(List<GeoLine3> edges, Tolerance tolerance)
        {
            int count = edges.Count * 2;
            GeoPoint3[] ends = new GeoPoint3[count];

            for (int i = 0; i < edges.Count; i++)
            {
                ends[2 * i] = edges[i].StartPoint;
                ends[2 * i + 1] = edges[i].EndPoint;
            }

            // A point on an edge lies within the tolerance of it, so inside its box grown by the tolerance; the
            // box is grown a hair more so that rounding cannot shut out an end the exact test would take.
            double reach = tolerance.EqualPoint * (1.0 + 1E-6) + 1E-12;

            // The ends sorted along an axis, so that the ones that could lie on an edge are found in a short run rather
            // than by testing every end against every edge: the axis that leaves the fewest in the runs. Along an axis
            // the edges stand square to, as the rim of a cap square to X, every end falls in the run of every edge, and
            // the 16 384 edges of a drum's cap took half a second. A cut position does not depend on the order the ends
            // are met in, since the split sorts them.
            double[] along = new double[count];
            int axis = 0;
            long fewest = long.MaxValue;

            for (int candidate = 0; candidate < 3; candidate++)
            {
                for (int i = 0; i < count; i++)
                {
                    along[i] = Coordinate(ends[i], candidate);
                }

                Array.Sort(along);
                long tried = 0;

                foreach (GeoLine3 edge in edges)
                {
                    double a = Coordinate(edge.StartPoint, candidate), b = Coordinate(edge.EndPoint, candidate);
                    tried += UpperBound(along, Math.Max(a, b) + reach) - LowerBound(along, Math.Min(a, b) - reach);
                }

                if (tried < fewest)
                {
                    fewest = tried;
                    axis = candidate;
                }
            }

            for (int i = 0; i < count; i++)
            {
                along[i] = Coordinate(ends[i], axis);
            }

            Array.Sort(along, ends);

            List<GeoLine3> resolved = new List<GeoLine3>(edges.Count);
            List<double> cuts = new List<double>();

            foreach (GeoLine3 edge in edges)
            {
                cuts.Clear();

                GeoPoint3 a = edge.StartPoint, b = edge.EndPoint;
                double minX = Math.Min(a.X, b.X) - reach, maxX = Math.Max(a.X, b.X) + reach;
                double minY = Math.Min(a.Y, b.Y) - reach, maxY = Math.Max(a.Y, b.Y) + reach;
                double minZ = Math.Min(a.Z, b.Z) - reach, maxZ = Math.Max(a.Z, b.Z) + reach;
                double high = Math.Max(Coordinate(a, axis), Coordinate(b, axis)) + reach;

                for (int k = LowerBound(along, Math.Min(Coordinate(a, axis), Coordinate(b, axis)) - reach); k < count && along[k] <= high; k++)
                {
                    GeoPoint3 end = ends[k];

                    if (end.X < minX || end.X > maxX || end.Y < minY || end.Y > maxY || end.Z < minZ || end.Z > maxZ)
                    {
                        continue;
                    }

                    if (Containment3.IsPointOn(edge, end, tolerance))
                    {
                        cuts.Add(Parametrization3.GetDistanceAtPoint(edge, end));
                    }
                }

                // The split drops positions at or beyond either end and merges those closer together than
                // the tolerance, so an edge nothing lands inside comes back as itself.
                resolved.AddRange(Splition3.SplitAtDistances(edge, cuts, tolerance));
            }

            edges.Clear();
            edges.AddRange(resolved);
        }

        private static double Coordinate(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        /// <summary>
        /// Gets the first position in a sorted array holding a value larger than a given one.
        /// </summary>
        private static int UpperBound(double[] sorted, double value)
        {
            int low = 0, high = sorted.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (sorted[middle] <= value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Gets the first position in a sorted array holding a value no smaller than a given one.
        /// </summary>
        private static int LowerBound(double[] sorted, double value)
        {
            int low = 0, high = sorted.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (sorted[middle] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Removes pairs of edges that run the same stretch in opposite directions.
        /// </summary>
        /// <remarks>
        /// Such a pair is an edge in the middle of the surviving surface rather than on the rim of the cut:
        /// two faces that both stayed on this side share it, so each traverses it once and the two
        /// contributions cancel. Leaving them in would send the chaining off along an edge that is not part
        /// of the section.
        /// <para>
        /// The edges are first cut at one another's ends, so that a stretch two faces divided differently
        /// still cancels. See <see cref="SplitAtEdgeEnds"/>.
        /// </para>
        /// </remarks>
        internal static void CancelOpposedEdges(List<GeoLine3> edges, Tolerance tolerance) => CancelOpposedEdges(edges, tolerance, out _);

        /// <summary>
        /// Removes pairs of edges that run the same stretch in opposite directions, and says how much area the pairs that
        /// cancelled leave between them.
        /// </summary>
        /// <param name="edges">The edges, changed in place.</param>
        /// <param name="tolerance">The tolerance the ends of two edges that cancel meet within.</param>
        /// <param name="open">
        /// The area of the four-sided figures each pair that cancelled makes with its ends, together; nought when none
        /// cancelled. One edge run both ways by the two faces sharing it leaves the rounding of the points a cut put on it;
        /// two edges a hair apart, the two sides of a sliver, leave the sliver.
        /// </param>
        /// <remarks>See <see cref="CancelOpposedEdges(List{GeoLine3}, Tolerance)"/>.</remarks>
        internal static void CancelOpposedEdges(List<GeoLine3> edges, Tolerance tolerance, out double open)
        {
            open = 0.0;
            SplitAtEdgeEnds(edges, tolerance);

            bool[] dropped = new bool[edges.Count];

            // The edge that cancels another starts where the other ends, so the edges are filed by their start
            // and only those starting near an end are tried — in the same order, and against the same test, as
            // trying every later edge, so the same pairs cancel.
            PointGrid starts = new PointGrid(tolerance);

            for (int j = 0; j < edges.Count; j++)
            {
                starts.Add(edges[j].StartPoint, j);
            }

            List<int> near = new List<int>();

            for (int i = 0; i < edges.Count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                starts.Near(edges[i].EndPoint, near);

                foreach (int j in near)
                {
                    if (j <= i || dropped[j])
                    {
                        continue;
                    }

                    if (edges[i].StartPoint.IsEqualTo(edges[j].EndPoint, tolerance) &&
                        edges[i].EndPoint.IsEqualTo(edges[j].StartPoint, tolerance))
                    {
                        dropped[i] = true;
                        dropped[j] = true;

                        // The figure the two run round, its area half the cross of its diagonals.
                        GeoVector3 across = edges[i].StartPoint.GetVectorTo(edges[j].StartPoint);
                        GeoVector3 back = edges[i].EndPoint.GetVectorTo(edges[j].EndPoint);
                        open += 0.5 * across.CrossProduct(back).Length;
                        break;
                    }
                }
            }

            for (int i = edges.Count - 1; i >= 0; i--)
            {
                if (dropped[i])
                {
                    edges.RemoveAt(i);
                }
            }
        }
    }
}
