using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
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
            List<GeoPolygon3> polygons = new List<GeoPolygon3>();

            foreach (List<GeoPoint3> loop in loops)
            {
                GeoPolygon3 polygon = TryBuildPolygon(loop, orientation, tolerance);

                if (polygon != null)
                {
                    polygons.Add(polygon);
                }
            }

            List<GeoFace3> faces = new List<GeoFace3>();
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

                faces.Add(new GeoFace3(polygons[i], holes, tolerance));
            }

            return faces;
        }

        /// <summary>
        /// Checks whether one loop lies inside another. The two must not cross.
        /// </summary>
        /// <remarks>
        /// Loops that do not cross are either wholly inside or wholly outside one another, so one vertex
        /// settles it. Vertices sitting on the other loop say nothing, which is why the scan keeps looking
        /// until it finds one that does.
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

            return false;
        }

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
                return piece.Normal.IsCodirectionalTo(orientation, tolerance) ? piece : piece.Flip();
            }
            catch (ArgumentException)
            {
                // Slivers along the cut are the expected failure here: fewer than three distinct vertices,
                // or three collinear ones. Neither is a piece worth reporting.
                return null;
            }
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
            List<GeoPoint3> ends = new List<GeoPoint3>(edges.Count * 2);

            foreach (GeoLine3 edge in edges)
            {
                ends.Add(edge.StartPoint);
                ends.Add(edge.EndPoint);
            }

            List<GeoLine3> resolved = new List<GeoLine3>(edges.Count);
            List<double> cuts = new List<double>();

            foreach (GeoLine3 edge in edges)
            {
                cuts.Clear();

                foreach (GeoPoint3 end in ends)
                {
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
        internal static void CancelOpposedEdges(List<GeoLine3> edges, Tolerance tolerance)
        {
            SplitAtEdgeEnds(edges, tolerance);

            bool[] dropped = new bool[edges.Count];

            for (int i = 0; i < edges.Count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (dropped[j])
                    {
                        continue;
                    }

                    if (edges[i].StartPoint.IsEqualTo(edges[j].EndPoint, tolerance) &&
                        edges[i].EndPoint.IsEqualTo(edges[j].StartPoint, tolerance))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
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
