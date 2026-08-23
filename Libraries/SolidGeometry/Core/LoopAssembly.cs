using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
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
        /// <param name="tolerance">The tolerance deciding whether two endpoints are the same point.</param>
        /// <param name="loops">The closed loops the edges form.</param>
        /// <returns>false when any chain came back open, meaning the edges do not close.</returns>
        /// <remarks>
        /// An open chain is a failure rather than a partial answer: a rim that does not close cannot be
        /// capped and a set of outlines that does not close is not a face, so guessing at either would
        /// produce geometry that looks right and is not.
        /// </remarks>
        internal static bool TryChainLoops(List<GeoLine3> edges, Tolerance tolerance, out List<List<GeoPoint3>> loops)
        {
            loops = new List<List<GeoPoint3>>();

            foreach (GeoPolyline3 chain in Merge3.Join(edges, tolerance))
            {
                if (!chain.StartPoint.IsEqualTo(chain.EndPoint, tolerance))
                {
                    return false;
                }

                loops.Add(new List<GeoPoint3>(chain.Vertices));
            }

            return loops.Count > 0;
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
        /// Removes pairs of edges that run the same stretch in opposite directions.
        /// </summary>
        /// <remarks>
        /// Such a pair is an edge in the middle of the surviving surface rather than on the rim of the cut:
        /// two faces that both stayed on this side share it, so each traverses it once and the two
        /// contributions cancel. Leaving them in would send the chaining off along an edge that is not part
        /// of the section.
        /// </remarks>
        internal static void CancelOpposedEdges(List<GeoLine3> edges, Tolerance tolerance)
        {
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
