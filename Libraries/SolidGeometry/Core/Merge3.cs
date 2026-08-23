using System;
using System.Collections.Generic;
using CommonGeometry;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for combining 3D curves that meet end to end.
    /// <para>
    /// <c>Consecutive...</c> takes the pieces in the order given and only ever joins a piece to the one
    /// after it, so a break in the sequence starts a new run. <c>Join</c> ignores the order and
    /// the direction of each piece and reassembles whatever chains the set actually forms, which is what
    /// a bag of edges out of a model needs.
    /// </para>
    /// </summary>
    public static class Merge3
    {
        #region Lines

        /// <summary>
        /// Merges runs of consecutive collinear segments, using the default tolerance.
        /// </summary>
        public static GeoLine3[] ConsecutiveLines(IEnumerable<GeoLine3> lines) => ConsecutiveLines(lines, Tolerance.Global);

        /// <summary>
        /// Merges runs of consecutive collinear segments, within a tolerance.
        /// </summary>
        /// <param name="lines">The segments, in order along the path.</param>
        /// <param name="tolerance">The tolerance deciding contact and collinearity.</param>
        /// <returns>The segments with each collinear run replaced by the single segment spanning it.</returns>
        /// <remarks>
        /// Two segments are merged only when the end of one meets the start of the next and their
        /// directions agree. Anti-parallel directions are not merged even though they are parallel: a
        /// segment doubling back on itself covers the same ground twice, and replacing the pair with the
        /// span between its outer endpoints would silently lose that.
        /// </remarks>
        public static GeoLine3[] ConsecutiveLines(IEnumerable<GeoLine3> lines, Tolerance tolerance)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            List<GeoLine3> merged = new List<GeoLine3>();

            foreach (GeoLine3 line in lines)
            {
                if (line.IsDegenerate(tolerance))
                {
                    continue;
                }

                if (merged.Count == 0)
                {
                    merged.Add(line);
                    continue;
                }

                GeoLine3 previous = merged[merged.Count - 1];

                bool meets = previous.EndPoint.IsEqualTo(line.StartPoint, tolerance);
                bool sameWay = Parallel3.IsCodirectional(previous.Direction, line.Direction, tolerance);

                if (meets && sameWay)
                {
                    merged[merged.Count - 1] = new GeoLine3(previous.StartPoint, line.EndPoint);
                }
                else
                {
                    merged.Add(line);
                }
            }

            return merged.ToArray();
        }

        #endregion

        #region Polylines

        /// <summary>
        /// Concatenates polylines that meet end to start into a single chain, using the default tolerance.
        /// </summary>
        public static GeoPolyline3 Polylines(IEnumerable<GeoPolyline3> polylines) => Polylines(polylines, Tolerance.Global);

        /// <summary>
        /// Concatenates polylines that meet end to start into a single chain, within a tolerance.
        /// </summary>
        /// <param name="polylines">The chains, in order.</param>
        /// <param name="tolerance">The tolerance deciding contact.</param>
        /// <returns>One chain covering all of them.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the sequence is empty, or when a chain does not start where the previous one ended.
        /// </exception>
        /// <remarks>
        /// This is the strict form: it will not reorder or reverse anything, and a gap is an error rather
        /// than something to bridge. Use <c>Join</c> when the pieces arrive in no particular order.
        /// </remarks>
        public static GeoPolyline3 Polylines(IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
        {
            if (polylines == null)
            {
                throw new ArgumentNullException(nameof(polylines));
            }

            List<GeoPoint3> vertices = new List<GeoPoint3>();

            foreach (GeoPolyline3 polyline in polylines)
            {
                if (polyline == null)
                {
                    throw new ArgumentException("Cannot merge a null polyline.", nameof(polylines));
                }

                if (vertices.Count == 0)
                {
                    vertices.AddRange(polyline.Vertices);
                    continue;
                }

                if (!vertices[vertices.Count - 1].IsEqualTo(polyline.StartPoint, tolerance))
                {
                    throw new ArgumentException("Each polyline must start where the previous one ended.", nameof(polylines));
                }

                for (int i = 1; i < polyline.VertexCount; i++)
                {
                    vertices.Add(polyline[i]);
                }
            }

            if (vertices.Count == 0)
            {
                throw new ArgumentException("There is nothing to merge.", nameof(polylines));
            }

            return new GeoPolyline3(vertices);
        }

        /// <summary>
        /// Merges runs of consecutive polylines that meet end to start, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] ConsecutivePolylines(IEnumerable<GeoPolyline3> polylines) => ConsecutivePolylines(polylines, Tolerance.Global);

        /// <summary>
        /// Merges runs of consecutive polylines that meet end to start, within a tolerance.
        /// </summary>
        /// <returns>
        /// One chain per unbroken run. A gap in the sequence starts a new chain rather than raising an
        /// error, which is the difference between this and <see cref="Polylines(IEnumerable{GeoPolyline3}, Tolerance)"/>.
        /// </returns>
        public static GeoPolyline3[] ConsecutivePolylines(IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
        {
            if (polylines == null)
            {
                throw new ArgumentNullException(nameof(polylines));
            }

            List<GeoPolyline3> results = new List<GeoPolyline3>();
            List<GeoPoint3> current = new List<GeoPoint3>();

            foreach (GeoPolyline3 polyline in polylines)
            {
                if (polyline == null)
                {
                    throw new ArgumentException("Cannot merge a null polyline.", nameof(polylines));
                }

                if (current.Count == 0)
                {
                    current.AddRange(polyline.Vertices);
                    continue;
                }

                if (current[current.Count - 1].IsEqualTo(polyline.StartPoint, tolerance))
                {
                    for (int i = 1; i < polyline.VertexCount; i++)
                    {
                        current.Add(polyline[i]);
                    }
                }
                else
                {
                    results.Add(new GeoPolyline3(current));
                    current = new List<GeoPoint3>(polyline.Vertices);
                }
            }

            if (current.Count > 0)
            {
                results.Add(new GeoPolyline3(current));
            }

            return results.ToArray();
        }

        #endregion

        #region Join

        /// <summary>
        /// Reassembles a set of segments into chains, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] Join(IEnumerable<GeoLine3> lines) => Join(lines, Tolerance.Global);

        /// <summary>
        /// Reassembles a set of segments into chains, within a tolerance.
        /// </summary>
        public static GeoPolyline3[] Join(IEnumerable<GeoLine3> lines, Tolerance tolerance)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            List<GeoPolyline3> pieces = new List<GeoPolyline3>();

            foreach (GeoLine3 line in lines)
            {
                if (!line.IsDegenerate(tolerance))
                {
                    pieces.Add(new GeoPolyline3(line.StartPoint, line.EndPoint));
                }
            }

            return Join(pieces, tolerance);
        }

        /// <summary>
        /// Reassembles a set of chains into as few chains as possible, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] Join(IEnumerable<GeoPolyline3> polylines) => Join(polylines, Tolerance.Global);

        /// <summary>
        /// Reassembles a set of chains into as few chains as possible, within a tolerance.
        /// </summary>
        /// <param name="polylines">The pieces, in any order and running either way.</param>
        /// <param name="tolerance">The tolerance deciding whether two endpoints are the same point.</param>
        /// <returns>One chain per connected run of pieces.</returns>
        /// <remarks>
        /// Each piece is used exactly once. Starting from an unused piece, the walk extends forwards from
        /// its end and then backwards from its start, reversing pieces as needed, until nothing more
        /// attaches; then the next unused piece starts a new chain. Where three or more pieces meet at a
        /// point the walk takes the first one it finds, since there is no ground for preferring another,
        /// so a branching set comes back as several chains rather than one.
        /// </remarks>
        public static GeoPolyline3[] Join(IEnumerable<GeoPolyline3> polylines, Tolerance tolerance)
        {
            if (polylines == null)
            {
                throw new ArgumentNullException(nameof(polylines));
            }

            List<GeoPolyline3> pieces = new List<GeoPolyline3>();

            foreach (GeoPolyline3 polyline in polylines)
            {
                if (polyline == null)
                {
                    throw new ArgumentException("Cannot join a null polyline.", nameof(polylines));
                }

                pieces.Add(polyline);
            }

            bool[] used = new bool[pieces.Count];
            List<GeoPolyline3> results = new List<GeoPolyline3>();

            for (int seed = 0; seed < pieces.Count; seed++)
            {
                if (used[seed])
                {
                    continue;
                }

                used[seed] = true;
                List<GeoPoint3> chain = new List<GeoPoint3>(pieces[seed].Vertices);

                ExtendForwards(pieces, used, chain, tolerance);

                // Growing backwards is the same problem reversed, so the chain is turned around, grown
                // forwards again, and turned back.
                chain.Reverse();
                ExtendForwards(pieces, used, chain, tolerance);
                chain.Reverse();

                results.Add(new GeoPolyline3(chain));
            }

            return results.ToArray();
        }

        /// <summary>
        /// Keeps attaching unused pieces to the end of a chain for as long as any of them fits.
        /// </summary>
        private static void ExtendForwards(List<GeoPolyline3> pieces, bool[] used, List<GeoPoint3> chain, Tolerance tolerance)
        {
            bool grew = true;

            while (grew)
            {
                grew = false;
                GeoPoint3 tail = chain[chain.Count - 1];

                for (int i = 0; i < pieces.Count; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }

                    GeoPolyline3 candidate = pieces[i];

                    if (tail.IsEqualTo(candidate.StartPoint, tolerance))
                    {
                        Append(chain, candidate);
                    }
                    else if (tail.IsEqualTo(candidate.EndPoint, tolerance))
                    {
                        Append(chain, candidate.Reverse());
                    }
                    else
                    {
                        continue;
                    }

                    used[i] = true;
                    grew = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Adds a piece to a chain, skipping the vertex the two already share.
        /// </summary>
        private static void Append(List<GeoPoint3> chain, GeoPolyline3 piece)
        {
            for (int i = 1; i < piece.VertexCount; i++)
            {
                chain.Add(piece[i]);
            }
        }

        #endregion

        #region Coplanar faces

        /// <summary>
        /// Merges the faces of a set that share a plane and touch, using the default tolerance.
        /// </summary>
        public static GeoFace3[] CoplanarFaces(IEnumerable<GeoFace3> faces) => CoplanarFaces(faces, Tolerance.Global);

        /// <summary>
        /// Merges the faces of a set that share a plane and touch, within a tolerance.
        /// </summary>
        /// <param name="faces">The faces to consider.</param>
        /// <param name="tolerance">The tolerance deciding which faces share a plane and which edges meet.</param>
        /// <returns>The faces with each touching coplanar group replaced by the single face covering it.</returns>
        /// <remarks>
        /// Cutting a body leaves its surface more finely divided than it needs to be: a face split in two by
        /// a plane comes back as two faces even on the half where the cut did not really separate anything.
        /// Repeated cutting compounds that, and the face count grows without the shape changing. This puts
        /// the surface back into as few faces as describe it.
        /// <para>
        /// Faces are grouped by the oriented plane they lie on, so a face and one facing the other way are
        /// never merged: they are different surfaces that happen to be flat in the same place. Within a
        /// group, the edges shared by two faces cancel out and what is left is the outline of the union,
        /// which may be several separate outlines and may have one inside another — merging a ring of faces
        /// leaves a hole in the middle, and that hole is kept as a hole.
        /// </para>
        /// <para>
        /// Faces in the same plane that do not touch are left as separate faces, since nothing joins them.
        /// </para>
        /// <para>
        /// Two faces count as touching only where they share a whole edge. A face whose edge runs the
        /// length of two edges of its neighbours meets them at a T-junction, and nothing cancels there, so
        /// that particular join does not happen and the faces come back separate. The rest of the group
        /// still merges normally, and the total area is unchanged either way: merging under-joins rather
        /// than guessing at an outline. Surfaces that come out of cutting are free of T-junctions, since a
        /// cut divides both sides of every edge it crosses.
        /// </para>
        /// <para>
        /// Vertices left in the middle of a straight run are kept rather than tidied away. They carry no
        /// shape, but removing them would leave the merged face meeting its neighbours at a T-junction
        /// wherever one of them still has the vertex, and a surface with a T-junction no longer reports
        /// itself closed.
        /// </para>
        /// </remarks>
        public static GeoFace3[] CoplanarFaces(IEnumerable<GeoFace3> faces, Tolerance tolerance)
        {
            if (faces == null)
            {
                throw new ArgumentNullException(nameof(faces));
            }

            List<GeoPlane3> planes = new List<GeoPlane3>();
            List<List<GeoFace3>> groups = new List<List<GeoFace3>>();

            foreach (GeoFace3 face in faces)
            {
                if (face == null)
                {
                    throw new ArgumentException("Cannot merge a null face.", nameof(faces));
                }

                GeoPlane3 plane = face.GetPlane();
                int group = -1;

                for (int i = 0; i < planes.Count; i++)
                {
                    if (planes[i].IsEqualTo(plane, tolerance))
                    {
                        group = i;
                        break;
                    }
                }

                if (group < 0)
                {
                    planes.Add(plane);
                    groups.Add(new List<GeoFace3>());
                    group = groups.Count - 1;
                }

                groups[group].Add(face);
            }

            List<GeoFace3> merged = new List<GeoFace3>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Count == 1)
                {
                    // Nothing to join it to, and rebuilding it would only risk changing it.
                    merged.Add(groups[i][0]);
                    continue;
                }

                merged.AddRange(MergeGroup(groups[i], planes[i], tolerance));
            }

            return merged.ToArray();
        }

        /// <summary>
        /// Merges the faces of one coplanar group into as few faces as cover them.
        /// </summary>
        /// <remarks>
        /// The union of a set of coplanar faces is bounded by exactly those edges that belong to one face
        /// only: an edge two of them share is interior to the union, and it appears twice among the
        /// collected edges running in opposite directions, so cancelling opposed pairs leaves precisely the
        /// outline. If what is left does not close into loops the group was not a well formed surface, and
        /// the faces are handed back untouched rather than replaced by a guess.
        /// </remarks>
        private static IEnumerable<GeoFace3> MergeGroup(List<GeoFace3> group, GeoPlane3 plane, Tolerance tolerance)
        {
            List<GeoLine3> edges = new List<GeoLine3>();

            foreach (GeoFace3 face in group)
            {
                foreach (IReadOnlyList<GeoPoint3> ring in LoopAssembly.EnumerateMaterialRings(face))
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        edges.Add(new GeoLine3(ring[i], ring[(i + 1) % ring.Count]));
                    }
                }
            }

            LoopAssembly.CancelOpposedEdges(edges, tolerance);

            if (edges.Count < 3)
            {
                return group;
            }

            if (!LoopAssembly.TryChainLoops(edges, tolerance, out List<List<GeoPoint3>> loops))
            {
                return group;
            }

            List<GeoFace3> assembled = LoopAssembly.AssembleFaces(loops, plane.Normal, tolerance);

            return assembled.Count > 0 ? assembled : (IEnumerable<GeoFace3>)group;
        }

        /// <summary>
        /// Merges the coplanar faces of a solid, using the default tolerance.
        /// </summary>
        public static GeoSolid3 CoplanarFaces(GeoSolid3 solid) => CoplanarFaces(solid, Tolerance.Global);

        /// <summary>
        /// Merges the coplanar faces of a solid, within a tolerance.
        /// </summary>
        /// <returns>
        /// The same body described by fewer faces, or the body unchanged when nothing could be merged.
        /// </returns>
        /// <remarks>
        /// The openings are simplified along with the outer surface. A body whose faces cannot be reduced
        /// below four is handed back as it was, since fewer than four flat faces cannot enclose a volume.
        /// </remarks>
        public static GeoSolid3 CoplanarFaces(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoFace3[] merged = CoplanarFaces(solid.Faces, tolerance);

            if (merged.Length < 4)
            {
                return solid;
            }

            List<GeoSolid3> openings = new List<GeoSolid3>();

            foreach (GeoSolid3 opening in solid.Openings)
            {
                openings.Add(CoplanarFaces(opening, tolerance));
            }

            return new GeoSolid3(merged, openings);
        }

        #endregion
    }
}
